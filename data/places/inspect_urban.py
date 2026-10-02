import zipfile
from pathlib import Path
import geopandas as gpd

z = Path("/data/places/13308773.zip")
work = Path("/tmp/urban")
work.mkdir(exist_ok=True)

print("ZIP:", z)
print("EXISTS:", z.exists())

with zipfile.ZipFile(z) as archive:
    print("\nFILES IN ZIP:")
    for f in archive.namelist():
        print(" ", f)
    archive.extractall(work)

shps = list(work.rglob("*.shp"))

print("\nSHAPEFILES:", len(shps))

for shp in shps:
    g = gpd.read_file(shp)

    print("\nFILE:", shp)
    print("ROWS:", len(g))
    print("CRS:", g.crs)
    print("GEOMETRY:", g.geometry.geom_type.value_counts().to_dict())
    print("COLUMNS:", list(g.columns))
    print("\nSAMPLE:")
    print(g.head(10).to_string())
