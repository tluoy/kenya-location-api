import json
import os
import re
import time
from pathlib import Path

import geopandas as gpd
import psycopg
from psycopg.types.json import Json


DB = os.getenv(
    "DATABASE_URL",
    "postgresql://location:REMOVED_CREDENTIAL@db:5432/kenya_location"
)

SOURCE_ZIP = Path("/data/places/13308773.zip")
SOURCE = "Kenya Urban Centres 2019"
SOURCE_VERSION = "13308773"


def normalize_name(value):
    return re.sub(r"\s+", " ", str(value).strip()).lower()


def wait_for_db():
    for attempt in range(30):
        try:
            with psycopg.connect(DB):
                print("Database connection ready.")
                return
        except Exception as exc:
            print(f"Waiting for database... {attempt + 1}/30: {exc}")
            time.sleep(2)

    raise RuntimeError("Database did not become available.")


def load_dataset():
    if not SOURCE_ZIP.exists():
        raise FileNotFoundError(
            f"Urban areas dataset not found: {SOURCE_ZIP}"
        )

    import zipfile

    work = Path("/tmp/urban-ingestion")
    work.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(SOURCE_ZIP) as archive:
        archive.extractall(work)

    shp_files = list(work.rglob("*.shp"))

    if len(shp_files) != 1:
        raise RuntimeError(
            f"Expected exactly one shapefile, found {len(shp_files)}"
        )

    shp = shp_files[0]

    print(f"Reading urban areas: {shp}")

    gdf = gpd.read_file(shp)

    if "Name" not in gdf.columns:
        raise RuntimeError(
            f"Expected 'Name' column. Found: {list(gdf.columns)}"
        )

    if gdf.crs is None:
        raise RuntimeError("Dataset has no CRS.")

    gdf = gdf.to_crs(4326)

    return gdf


def clean_geometry(geometry):
    if geometry is None or geometry.is_empty:
        return None

    if not geometry.is_valid:
        geometry = geometry.buffer(0)

    if geometry.is_empty:
        return None

    if geometry.geom_type == "Polygon":
        from shapely.geometry import MultiPolygon

        geometry = MultiPolygon([geometry])

    elif geometry.geom_type != "MultiPolygon":
        return None

    from shapely import force_2d

    geometry = force_2d(geometry)

    if not geometry.is_valid:
        geometry = geometry.buffer(0)

    if geometry.is_empty:
        return None

    return geometry


def ingest():
    wait_for_db()

    gdf = load_dataset()

    print(f"Urban areas discovered: {len(gdf)}")

    records = []

    for index, row in gdf.iterrows():
        name = str(row["Name"]).strip()

        if not name:
            continue

        geometry = clean_geometry(row.geometry)

        if geometry is None:
            print(f"Skipping invalid geometry: {name}")
            continue

        centroid = geometry.representative_point()

        place_id = f"KE-UA-{index + 1:04d}"

        attributes = {
            "source_index": int(index),
        }

        records.append(
            (
                place_id,
                name,
                normalize_name(name),
                "urban_area",
                centroid.wkt,
                geometry.wkt,
                SOURCE,
                SOURCE_VERSION,
                json.dumps(attributes),
            )
        )

    print(f"Prepared urban areas: {len(records)}")

    with psycopg.connect(DB) as conn:
        with conn.cursor() as cur:

            cur.execute(
                """
                DELETE FROM places
                WHERE source = %s
                """,
                (SOURCE,),
            )

            for record in records:
                (
                    place_id,
                    name,
                    normalized_name,
                    place_type,
                    centroid_wkt,
                    geometry_wkt,
                    source,
                    source_version,
                    attributes,
                ) = record

                cur.execute(
                    """
                    INSERT INTO places (
                        id,
                        name,
                        normalized_name,
                        place_type,
                        centroid,
                        geometry,
                        source,
                        source_version,
                        attributes
                    )
                    VALUES (
                        %s,
                        %s,
                        %s,
                        %s,
                        ST_GeomFromText(%s, 4326),
                        ST_Multi(
                            ST_GeomFromText(%s, 4326)
                        ),
                        %s,
                        %s,
                        %s
                    )
                    """,
                    (
                        place_id,
                        name,
                        normalized_name,
                        place_type,
                        centroid_wkt,
                        geometry_wkt,
                        source,
                        source_version,
                        Json(json.loads(attributes)),
                    ),
                )

            conn.commit()

    print(f"Imported urban areas: {len(records)}")


def assign_admin_units():
    print("Assigning urban areas to administrative hierarchy...")

    with psycopg.connect(DB) as conn:
        with conn.cursor() as cur:
            cur.execute(
                """
                UPDATE places p
                SET admin_unit_id = a.id,
                    updated_at = now()
                FROM admin_units a
                WHERE a.level = 3
                  AND ST_Covers(a.geometry, p.centroid)
                  AND p.source = %s
                """,
                (SOURCE,),
            )

            assigned = cur.rowcount
            conn.commit()

    print(f"Urban areas assigned to wards: {assigned}")


def main():
    ingest()
    assign_admin_units()

    with psycopg.connect(DB) as conn:
        with conn.cursor() as cur:
            cur.execute(
                """
                SELECT
                    COUNT(*) AS total,
                    COUNT(admin_unit_id) AS assigned,
                    COUNT(*) FILTER (
                        WHERE admin_unit_id IS NULL
                    ) AS unassigned
                FROM places
                WHERE source = %s
                """,
                (SOURCE,),
            )

            total, assigned, unassigned = cur.fetchone()

    print()
    print("Urban area summary")
    print("===================")
    print(f"Total:      {total}")
    print(f"Assigned:   {assigned}")
    print(f"Unassigned: {unassigned}")


if __name__ == "__main__":
    main()