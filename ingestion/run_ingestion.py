import ingest
import ingest_places


if __name__ == "__main__":
    print("========================================")
    print("KENYA LOCATION DATA INGESTION")
    print("========================================")

    print()
    print("1. Administrative boundaries")
    print("----------------------------------------")
    ingest.main()

    print()
    print("2. Places / urban areas")
    print("----------------------------------------")
    ingest_places.main()

    print()
    print("========================================")
    print("INGESTION COMPLETE")
    print("========================================")