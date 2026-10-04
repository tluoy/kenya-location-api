import os
from pathlib import Path

from azure.identity import DefaultAzureCredential
from azure.storage.blob import BlobServiceClient


GIS_ROOT = Path(os.getenv("GIS_ROOT", "/data/gis"))
PLACES_SOURCE_ZIP = Path(
    os.getenv("PLACES_SOURCE_ZIP", "/data/places/13308773.zip")
)

AZURE_STORAGE_ACCOUNT_URL = os.getenv("AZURE_STORAGE_ACCOUNT_URL")
AZURE_STORAGE_CONTAINER = os.getenv("AZURE_STORAGE_CONTAINER", "source-data")

GIS_BLOBS = [
    "gis/kenya-adm1.geojson",
    "gis/kenya-adm2.geojson",
    "gis/kenya-adm3.geojson",
]

PLACES_BLOB = "places/13308773.zip"


def download_blob(
    container_client,
    blob_name: str,
    destination: Path,
) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)

    print(f"Downloading {blob_name} -> {destination}")

    blob_client = container_client.get_blob_client(blob_name)

    with destination.open("wb") as output:
        download_stream = blob_client.download_blob()
        download_stream.readinto(output)


def download_sources() -> None:
    if not AZURE_STORAGE_ACCOUNT_URL:
        print("Azure Blob source configuration not present; using local files.")
        return

    print("Azure Blob source configuration detected.")
    print(f"Storage account: {AZURE_STORAGE_ACCOUNT_URL}")
    print(f"Container: {AZURE_STORAGE_CONTAINER}")

    credential = DefaultAzureCredential()
    service_client = BlobServiceClient(
        account_url=AZURE_STORAGE_ACCOUNT_URL,
        credential=credential,
    )

    container_client = service_client.get_container_client(
        AZURE_STORAGE_CONTAINER
    )

    for blob_name in GIS_BLOBS:
        download_blob(
            container_client,
            blob_name,
            GIS_ROOT / Path(blob_name).name,
        )

    download_blob(
        container_client,
        PLACES_BLOB,
        PLACES_SOURCE_ZIP,
    )


if __name__ == "__main__":
    download_sources()