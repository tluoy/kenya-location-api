# Kenya Location Intelligence API — Demo MVP

A production-oriented MVP foundation for Kenya location-aware applications. It provides:

- County / sub-county / ward / constituency-ready geographic hierarchy
- Deep administrative boundary ingestion (country → county → sub-county → division → location → sub-location → village) when boundary source data is available
- GPS reverse lookup using PostGIS point-in-polygon
- Nearest administrative unit fallback using PostGIS distance
- Name search
- Hierarchy lookup
- Swagger/OpenAPI
- Docker Compose with PostgreSQL + PostGIS
- Python GIS ingestion pipeline
- GitHub Actions CI
- Azure DevOps pipeline skeleton
- Azure Container Apps deployment skeleton
- A tiny browser demo at `/demo`

## Data sources

The MVP is designed around two public sources:

1. Open Admin Data Kenya: 47 counties, 290 sub-counties and 1,450 wards with WGS84 coordinates, CC-BY-4.0. https://github.com/open-admin-data/kenya-administrative-divisions
2. Kenya administrative boundary repository by leoouma: levels 0–6, including county, sub-county, division, location, sub-location and village boundary files; repository license is MIT. https://github.com/leoouma/KE_Admin_Boundaries

The application stores imported data locally in PostGIS; it does not depend on those public APIs at request time.

## Fast start

### Prerequisites

- Docker Desktop / Docker Engine
- Docker Compose
- Internet access from Docker during the first ingestion

### Start

```bash
docker compose up --build
```

Then open:

- Demo: http://localhost:8080/demo
- Swagger: http://localhost:8080/swagger
- Health: http://localhost:8080/health

The database starts with PostGIS. Docker Compose runs the local PostgreSQL/PostGIS database, ingestion service, and API.

The local ingestion service uses GIS and places source files mounted from the repository's `data/` directory. Azure uses a separate ingestion workflow where source files are stored in Azure Blob Storage and consumed by an Azure Container Apps Job.

### Database initialization

The database schema is initialized from the SQL files in `database/` when PostgreSQL creates a new data volume:

- `001_schema.sql` — core administrative-unit schema
- `002_seed_demo.sql` — seed/bootstrap placeholder
- `003_places_schema.sql` — places schema

These files are **initialization scripts, not migrations**. PostgreSQL does not re-run `/docker-entrypoint-initdb.d/` scripts when an existing `pgdata` volume is reused.

When the database schema evolves after the MVP baseline, the change must be introduced through an explicit migration/upgrade step rather than by adding another initialization script and assuming existing databases will apply it automatically.

## Development and deployment workflows

### Local development

The recommended local runtime is Docker Compose:

```bash
docker compose up -d --build
```

## API examples

```bash
curl 'http://localhost:8080/api/v1/geolocation/reverse?latitude=-0.2827&longitude=34.7519'

curl 'http://localhost:8080/api/v1/locations/search?q=Kakamega&limit=10'

curl 'http://localhost:8080/api/v1/locations/counties'
```

## Architecture

```text
                 Browser / Mobile 
                         |
                         v
                 ASP.NET Core API
                         |
                  PostgreSQL/PostGIS
                         ^
                         |
                 Python GIS ingestion
                         |
              Public GIS/data sources
```

## Important MVP boundary

This is a **demo-ready MVP**, not yet a nationally certified authoritative geocoder. Boundary datasets can differ by source/version and administrative/electoral concepts must not be mixed. Before production use, establish a signed-off source hierarchy and data-governance process, version every import, and add automated topology/hierarchy reconciliation against authoritative government sources.
