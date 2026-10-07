import json
import importlib
import os
import re
import time
from pathlib import Path

try:
    psycopg = importlib.import_module("psycopg")
except ImportError as exc:  # pragma: no cover - dependency validation
    raise RuntimeError(
        "The 'psycopg' package is required for database ingestion. "
        "Install it with `pip install psycopg[binary]`."
    ) from exc

try:
    from shapely.geometry import MultiPolygon
except ImportError as exc:  # pragma: no cover - dependency validation
    raise RuntimeError(
        "The 'shapely' package is required for GIS ingestion. "
        "Install it with `pip install shapely`."
    ) from exc


DB = os.getenv("DATABASE_URL")

if not DB:
    raise RuntimeError("DATABASE_URL is not configured.")

GIS_ROOT = Path(os.getenv("GIS_ROOT", "/data/gis"))

DATASETS = {
    1: {
        "name": "county",
        "file": GIS_ROOT / "kenya-adm1.geojson",
        "source": "geoBoundaries",
        "source_version": "gbOpen-current",
    },
    2: {
        "name": "sub-county",
        "file": GIS_ROOT / "kenya-adm2.geojson",
        "source": "geoBoundaries",
        "source_version": "gbOpen-current",
    },
    3: {
        "name": "ward",
        "file": GIS_ROOT / "kenya-adm3.geojson",
        "source": "geoBoundaries",
        "source_version": "gbOpen-current",
    },
}


def normalize(value):
    if value is None:
        return ""

    return re.sub(r"\s+", " ", str(value).strip().lower())


def wait_db():
    for attempt in range(60):
        try:
            with psycopg.connect(DB) as conn:
                with conn.cursor() as cur:
                    cur.execute("SELECT 1")

            print("Database ready.")
            return

        except Exception as exc:
            if attempt == 59:
                raise RuntimeError(
                    f"Database was not ready after retries: {exc}"
                ) from exc

            time.sleep(2)


def clean_geometry(geometry):
    if geometry is None or geometry.is_empty:
        return None

    if not geometry.is_valid:
        geometry = geometry.buffer(0)

    if geometry.is_empty:
        return None

    if geometry.geom_type == "Polygon":
        geometry = MultiPolygon([geometry])

    elif geometry.geom_type != "MultiPolygon":
        if geometry.geom_type == "GeometryCollection":
            polygons = [
                g
                for g in geometry.geoms
                if g.geom_type in ("Polygon", "MultiPolygon")
            ]

            if not polygons:
                return None

            geometry = polygons[0]

            if geometry.geom_type == "Polygon":
                geometry = MultiPolygon([geometry])

        else:
            return None

    return geometry


def find_name_column(gdf):
    candidates = [
        "shapeName",
        "shape_name",
        "name",
        "NAME",
        "NAME_1",
        "NAME_2",
        "NAME_3",
        "admin_name",
        "ADM_NAME",
    ]

    for column in candidates:
        if column in gdf.columns:
            return column

    # geoBoundaries normally has shapeName, but fail explicitly
    # rather than silently creating bad records.
    raise RuntimeError(
        "Could not identify administrative name column. "
        f"Available columns: {list(gdf.columns)}"
    )


def find_code_column(gdf):
    candidates = [
        "shapeID",
        "shapeISO",
        "shapeGroup",
        "GID_1",
        "GID_2",
        "GID_3",
        "GID",
        "code",
        "CODE",
    ]

    for column in candidates:
        if column in gdf.columns:
            return column

    return None


def json_safe_attributes(row):
    result = {}

    for key, value in row.items():
        if key == "geometry":
            continue

        if value is None:
            result[str(key)] = None
            continue

        try:
            if hasattr(value, "item"):
                value = value.item()
        except Exception:
            pass

        result[str(key)] = str(value)

    return result


def dataset_rows(level, config):
    try:
        import geopandas as gpd
    except ImportError as exc:  # pragma: no cover - dependency validation
        raise RuntimeError(
            "The 'geopandas' package is required for GIS ingestion. "
            "Install it with `pip install geopandas`."
        ) from exc

    path = config["file"]

    if not path.exists():
        raise FileNotFoundError(
            f"Required GIS dataset does not exist: {path}"
        )

    print()
    print("=" * 70)
    print(f"Loading ADM{level}: {config['name']}")
    print(f"File: {path}")
    print("=" * 70)

    gdf = gpd.read_file(path)

    print(f"Raw rows: {len(gdf)}")
    print(f"Source CRS: {gdf.crs}")
    print(f"Columns: {list(gdf.columns)}")

    if gdf.empty:
        raise RuntimeError(f"Dataset is empty: {path}")

    if gdf.crs is None:
        raise RuntimeError(
            f"Dataset has no CRS information: {path}"
        )

    gdf = gdf.to_crs(4326)

    name_column = find_name_column(gdf)
    code_column = find_code_column(gdf)

    print(f"Name column: {name_column}")
    print(f"Code column: {code_column}")
    print(f"Converted CRS: {gdf.crs}")

    imported = 0
    skipped = 0

    records = []

    for _, row in gdf.iterrows():
        geometry = clean_geometry(row.geometry)

        if geometry is None:
            skipped += 1
            continue

        name = str(row[name_column]).strip()

        if not name or name.lower() == "nan":
            skipped += 1
            continue

        code = None

        if code_column:
            value = row[code_column]

            if value is not None:
                value = str(value).strip()

                if value and value.lower() != "nan":
                    code = value

        # Prefer a stable source identifier.
        if code:
            unit_id = f"KE-L{level}-{normalize(code)}"
        else:
            unit_id = (
                f"KE-L{level}-"
                f"{normalize(name).replace(' ', '-')}"
            )

        centroid = geometry.representative_point()

        attributes = json_safe_attributes(row)

        records.append(
            (
                unit_id,
                code,
                name,
                normalize(name),
                level,
                config["name"],
                centroid.x,
                centroid.y,
                geometry.wkt,
                config["source"],
                config["source_version"],
                json.dumps(attributes),
            )
        )

        imported += 1

    print(f"Valid records: {imported}")
    print(f"Skipped records: {skipped}")

    return records


def upsert_records(conn, records):
    sql = """
    INSERT INTO admin_units (
        id,
        code,
        name,
        normalized_name,
        level,
        level_name,
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
        %s,
        %s,
        ST_SetSRID(
            ST_MakePoint(%s, %s),
            4326
        ),
        ST_SetSRID(
            ST_GeomFromText(%s),
            4326
        ),
        %s,
        %s,
        %s::jsonb
    )
    ON CONFLICT (id)
    DO UPDATE SET
        code = EXCLUDED.code,
        name = EXCLUDED.name,
        normalized_name = EXCLUDED.normalized_name,
        level = EXCLUDED.level,
        level_name = EXCLUDED.level_name,
        centroid = EXCLUDED.centroid,
        geometry = EXCLUDED.geometry,
        source = EXCLUDED.source,
        source_version = EXCLUDED.source_version,
        attributes = EXCLUDED.attributes,
        updated_at = now();
    """

    with conn.cursor() as cur:
        cur.executemany(sql, records)

    conn.commit()


def assign_parents(conn):
    """
    Establish:

        County -> Sub-County
        Sub-County -> Ward

    using spatial containment.

    Ward is deliberately kept as an ADM3/ward level rather
    than being mislabeled as a Division.
    """

    print()
    print("Assigning administrative parents...")

    with conn.cursor() as cur:

        cur.execute(
            """
            UPDATE admin_units
            SET parent_id = NULL
            WHERE level IN (2, 3);
            """
        )

        # Sub-county -> County
        cur.execute(
            """
            UPDATE admin_units child
            SET parent_id = parent.id
            FROM admin_units parent
            WHERE child.level = 2
              AND parent.level = 1
              AND ST_Covers(
                    parent.geometry,
                    child.centroid
                  );
            """
        )

        subcounty_without_parent = cur.rowcount

        print(
            f"Sub-county parent assignments: "
            f"{subcounty_without_parent}"
        )

        # Ward -> Sub-county
        cur.execute(
            """
            UPDATE admin_units child
            SET parent_id = parent.id
            FROM admin_units parent
            WHERE child.level = 3
              AND parent.level = 2
              AND ST_Covers(
                    parent.geometry,
                    child.centroid
                  );
            """
        )

        ward_assignments = cur.rowcount

        print(
            f"Ward parent assignments: "
            f"{ward_assignments}"
        )

    conn.commit()


def print_summary(conn):
    print()
    print("=" * 70)
    print("INGESTION SUMMARY")
    print("=" * 70)

    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT
                level,
                level_name,
                COUNT(*)
            FROM admin_units
            GROUP BY level, level_name
            ORDER BY level;
            """
        )

        rows = cur.fetchall()

        if not rows:
            print("No administrative units found.")
            return

        for level, level_name, count in rows:
            print(
                f"ADM{level:<2} "
                f"{level_name:<15} "
                f"{count:>6}"
            )

        cur.execute(
            """
            SELECT COUNT(*)
            FROM admin_units;
            """
        )

        total = cur.fetchone()[0]

        print("-" * 70)
        print(f"TOTAL ADMIN UNITS: {total}")


def main():
    print()
    print("KENYA LOCATION API - GIS INGESTION")
    print("=" * 70)

    wait_db()

    with psycopg.connect(DB) as conn:

        # Remove previously imported GIS administrative levels.
        # This makes ingestion deterministic and prevents duplicates
        # when the ingestion job is run repeatedly.
        with conn.cursor() as cur:
            # Places reference administrative units through admin_unit_id.
            # Clear those references before reloading the boundary hierarchy.
            cur.execute(
                """
                UPDATE places
                SET admin_unit_id = NULL,
                    updated_at = now()
                WHERE admin_unit_id IS NOT NULL;
                """
            )

            cur.execute(
                """
                DELETE FROM admin_units
                WHERE level IN (1, 2, 3);
                """
            )

        conn.commit()

        all_records = []

        for level in sorted(DATASETS):
            records = dataset_rows(
                level,
                DATASETS[level],
            )

            upsert_records(conn, records)

            all_records.extend(records)

        assign_parents(conn)

        print_summary(conn)

    print()
    print("=" * 70)
    print("INGESTION COMPLETE")
    print("=" * 70)

if __name__ == "__main__":
    main()