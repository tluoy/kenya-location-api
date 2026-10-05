# Azure Deployment

This directory contains deployment configuration for the Kenya Location API.

## Target Azure topology

* Azure Container Registry — application and ingestion images
* Azure Container Apps — long-running API
* Azure Container Apps Job — one-shot data ingestion
* Azure Database for PostgreSQL Flexible Server — persistent PostGIS database
* Azure Storage Account — source GIS and places data

## Architecture

```text
                         ┌──────────────────────────┐
                         │   Azure Blob Storage      │
                         │                          │
                         │  GIS GeoJSON             │
                         │  Urban Centres ZIP       │
                         └────────────┬─────────────┘
                                      │
                                      │ Managed Identity
                                      ▼
                         ┌──────────────────────────┐
                         │ Container Apps Job       │
                         │ kenya-location-ingestion │
                         │                          │
                         │ One-shot ingestion       │
                         └────────────┬─────────────┘
                                      │
                                      │ PostgreSQL
                                      ▼
                         ┌──────────────────────────┐
                         │ PostgreSQL Flexible      │
                         │ Server + PostGIS         │
                         │                          │
                         │ admin_units              │
                         │ places                   │
                         └────────────┬─────────────┘
                                      │
                                      │ PostgreSQL
                                      ▼
                         ┌──────────────────────────┐
                         │ Container App            │
                         │ Kenya Location API       │
                         │                          │
                         │ REST + spatial queries   │
                         └──────────────────────────┘

                         ┌──────────────────────────┐
                         │ Azure Container Registry │
                         │                          │
                         │ API image                │
                         │ Ingestion image          │
                         └──────────────────────────┘
```

## Deployment principles

* The API is a long-running containerized service.
* Data ingestion is a finite workload and runs as an Azure Container Apps Job.
* PostgreSQL/PostGIS is persistent infrastructure and is not deployed as part of the application container stack.
* Source GIS data is stored in Azure Blob Storage.
* Managed identities are preferred for Azure resource-to-resource authentication.
* Production secrets must be supplied through Azure configuration/secrets rather than committed to Git.
* Container Registry admin credentials remain disabled.
* Deployment changes are developed on a feature branch and promoted through `develop` and `main`.
* Infrastructure provisioning and data ingestion are separate operations.
* The ingestion Job can be manually triggered after infrastructure and source data are available.

## Current Azure development environment

Resource group:

```text
kenya-location-api-dev-rg
```

Region:

```text
South Africa North
```

### Container Registry

```text
kenyalocationapidev.azurecr.io
```

Repositories:

```text
kenya-location-api:dev
kenya-location-ingestion:dev
```

Registry authentication uses managed identities. The ACR admin user is disabled.

### Container Apps Environment

```text
kenya-location-api-dev-env
```

### API Container App

```text
kenya-location-api-dev
```

The API listens on port `8080` and is externally accessible through Azure Container Apps ingress.

### Ingestion Job

```text
kenya-location-api-dev-ingestion
```

The Job uses a manual trigger and is intended for finite ingestion workloads.

### PostgreSQL

Server:

```text
kenya-location-api-dev-db
```

Database:

```text
kenya_location
```

PostgreSQL major version:

```text
17
```

PostGIS is enabled in the database.

### Source Storage

Storage account:

```text
kenyalocationdevdata
```

Container:

```text
source-data
```

Expected source objects include:

```text
gis/kenya-adm1.geojson
gis/kenya-adm2.geojson
gis/kenya-adm3.geojson
places/13308773.zip
```

The ingestion Job uses its system-assigned managed identity with `Storage Blob Data Reader` access.

## Data ingestion

The normal ingestion flow is:

1. Build the ingestion image.
2. Push it to Azure Container Registry.
3. Upload source data to the `source-data` Blob container.
4. Start the `kenya-location-api-dev-ingestion` Job.
5. Wait for the execution to succeed.
6. Validate the API endpoints.

The ingestion Job does not run continuously.

## Validation

The Azure development environment has been provisioned and the API has been validated. Azure ingestion remains unresolved and is not yet considered end-to-end validated.

Validated functionality includes:

* API health
* County listing
* Place search
* Place detail
* Place geometry
* Administrative hierarchy linkage
* Reverse geolocation
* Nearby-place search
* Azure ingestion execution remains unresolved
Example validated place:

```text
Ahero
Ward: Ahero
Sub-county: Nyando
County: Kisumu
```

## Infrastructure as Code

`main.bicep` is the source of truth for Azure infrastructure.

Manual changes made during the initial MVP deployment should be reconciled into Bicep before the environment is considered reproducible.

The Bicep deployment must not be used to overwrite or recreate the existing development PostgreSQL server without an explicit migration/recovery plan.
