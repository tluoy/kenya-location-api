# Kenya Location Intelligence API

A containerised geospatial REST API for Kenya location-aware applications. It combines an ASP.NET Core API with PostgreSQL/PostGIS and a Python GIS ingestion pipeline. The repository includes local Docker Compose setup, integration tests, GitHub Actions CI, and Azure deployment infrastructure.

> **Current status:** The development Azure API and its health, metadata, and Swagger endpoints have been verified. CI is green. The integration suite has 13 non-rate-limit tests; rate limiting is exercised separately by CI with a deliberately low limit. This is an MVP, not a nationally certified authoritative geocoder. Treat Azure ingestion as a separate operational workflow and verify each Job execution before relying on a fresh import.

## Contents

- [What it does](#what-it-does)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Run locally with Docker Compose](#run-locally-with-docker-compose)
- [Test the API](#test-the-api)
- [Integration tests](#integration-tests)
- [Configuration and troubleshooting](#configuration-and-troubleshooting)
- [Azure development environment](#azure-development-environment)
- [Azure identities and access](#azure-identities-and-access)
- [Azure data ingestion](#azure-data-ingestion)
- [CI, releases, and branch workflow](#ci-releases-and-branch-workflow)
- [Repository layout](#repository-layout)
- [Data sources and limitations](#data-sources-and-limitations)
- [Security and operational notes](#security-and-operational-notes)

## What it does

- Administrative-location hierarchy and search.
- County listing and location detail/hierarchy lookup.
- Reverse geolocation from latitude/longitude, using PostGIS spatial queries.
- Nearby-location and nearby-place searches.
- Place search and place detail.
- Health/readiness endpoints and API metadata.
- Swagger/OpenAPI documentation.

The loaded dataset includes Kenya's 47 counties, 290 sub-counties, 1,452 wards, and urban-place records. Counts can change when source datasets or ingestion rules change; do not treat the counts as a substitute for validating a specific import.

## Architecture

### Local

```text
Browser / client / integration tests
                |
                v
      API container :8080
                |
                v
    PostgreSQL + PostGIS :5432
                ^
                |
       Python ingestion container
```

Docker Compose runs three services:

| Service | Responsibility |
|---|---|
| `db` | PostgreSQL with PostGIS; persists data in the `pgdata` Docker volume. |
| `ingestion` | One-shot Python/GeoPandas import of local GIS and places source files into PostGIS. It exits when the import finishes. |
| `api` | ASP.NET Core API, exposed on host port `8080`; waits for the database to be healthy and ingestion to complete successfully. |

### Azure

```text
Source files in private Blob Storage
               |
       user-assigned identity
               v
  Azure Container Apps ingestion Job
               |
               v
 PostgreSQL Flexible Server + PostGIS
               ^
               |
      Azure Container App (API)

 Azure Container Registry stores the API and ingestion images.
 GitHub Actions builds/tests and deploys release-tagged API images.
```

In Azure, the database is a managed PostgreSQL Flexible Server, not a container in the application stack. The API is a long-running Container App; ingestion is a manually triggered Container Apps Job.

## Prerequisites

For local development:

- Git.
- Docker Desktop on Windows/macOS, or Docker Engine plus the Docker Compose v2 plugin on Linux.
- .NET 10 SDK to build and run tests from the host.
- Network access to download the urban-centres source archive during CI or when preparing local data.
- PowerShell on Windows for the command examples below, or an equivalent shell.

For Azure deployment/operations:

- Azure CLI, logged into the correct subscription.
- Permissions to inspect/run Container Apps Jobs and view resource logs.
- GitHub repository access for workflow runs, environments, and release tags.
- Deployment credentials/secrets configured in GitHub/Azure. Never commit passwords, connection strings, access tokens, or credential files.

## Run locally with Docker Compose

Run commands from the repository root.

### 1. Check required data files

The Compose ingestion service mounts:

- `data/gis/` into `/data/gis`
- `data/places/` into `/data/places`
- the urban-centres archive at `data/places/13308773.zip`

The archive is available from the Figshare article used by this project:
`https://api.figshare.com/v2/articles/13308773/download`

If the archive is missing, download it before starting the stack. The GIS folder must contain the expected source files for the current ingestion script. If ingestion fails, check its logs before troubleshooting the API.

PowerShell example:

```powershell
New-Item -ItemType Directory -Force .\data\places | Out-Null
Invoke-WebRequest `
  -Uri "https://api.figshare.com/v2/articles/13308773/download" `
  -OutFile ".\data\places\13308773.zip"
```

### 2. Set a local database password

Set a shell-only password before using Compose. This is for local development only; it is not the Azure password.

```powershell
$env:POSTGRES_PASSWORD = "choose-a-local-development-password"
```

Compose passes this value to the database, ingestion container, and API. Do not commit it to `.env`, scripts, or source control. If you use a `.env` file for convenience, keep it untracked and out of version control.

### 3. Start the stack

```powershell
docker compose up -d --build
docker compose ps
docker compose logs --tail 100 db ingestion api
```

The first run can take longer because images need to build, PostgreSQL needs to initialize, and ingestion has to load the source data. The API depends on ingestion completing successfully, so it may not start if ingestion exits with an error.

### 4. Verify the service

```powershell
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod http://localhost:8080/api/v1/meta
```

Open these URLs in a browser:

- Swagger UI: http://localhost:8080/swagger
- Health: http://localhost:8080/health
- Liveness: http://localhost:8080/health/live
- Readiness: http://localhost:8080/health/ready
- Metadata: http://localhost:8080/api/v1/meta

### 5. Stop the stack

```powershell
docker compose down
```

This stops containers but retains the database volume. To **irreversibly delete the local database data** and re-run first-time initialization scripts on the next startup:

```powershell
docker compose down -v
```

Use `down -v` only when you intentionally want to discard local database contents. The SQL files mounted into `/docker-entrypoint-initdb.d/` are first-initialization scripts, not automatic migrations; PostgreSQL does not rerun them when an existing `pgdata` volume is reused.

## Test the API

Example PowerShell requests:

```powershell
Invoke-RestMethod "http://localhost:8080/api/v1/locations/counties"
Invoke-RestMethod "http://localhost:8080/api/v1/locations/search?q=Kakamega&limit=10"
Invoke-RestMethod "http://localhost:8080/api/v1/places/search?q=Kakamega"
Invoke-RestMethod "http://localhost:8080/api/v1/geolocation/reverse?latitude=-0.2827&longitude=34.7519"
```

The API contract is also available in Swagger. Use the deployed API for smoke tests, but do not run load or destructive tests against the shared development environment.

### Development Azure endpoint

Base URL:

`https://kenya-location-api-dev.grayforest-93e1dd53.southafricanorth.azurecontainerapps.io/`

Useful checks:

- `/health`
- `/health/live`
- `/health/ready`
- `/swagger`
- `/api/v1/meta`
- `/api/v1/locations/counties`

This is a development/demo endpoint. Availability, scale-to-zero wake-up time, and data freshness may vary.

## Integration tests

The test project is `tests/KenyaLocation.Api.IntegrationTests`. Tests use `API_BASE_URL` and default to `http://localhost:8080`.

Build and run the suite against a running local Compose API:

```powershell
$env:API_BASE_URL = "http://localhost:8080"
dotnet test .\tests\KenyaLocation.Api.IntegrationTests\KenyaLocation.Api.IntegrationTests.csproj
```

The local API must be healthy and loaded with data first. The integration tests exercise the HTTP API; they do not provision the database or start Docker Compose automatically.

### Why CI runs rate limiting separately

The production/default configuration is `60` permits per `60` seconds. The rate-limit integration test expects a 429 response after exceeding its configured permit limit. CI therefore:

1. Starts the Compose stack with `RATE_LIMIT_PERMIT_LIMIT=2` and `RATE_LIMIT_WINDOW_SECONDS=60`.
2. Runs only `RateLimitingTests`.
3. Recreates the API with the normal limit of 60.
4. Runs the remaining integration tests against the normal limit.
5. Stops the Compose stack and removes its temporary database volume.

This separation is intentional. Setting the limit to 2 for the whole suite causes unrelated tests from the same client/IP to receive 429 responses. Do not change the production default just to make the rate-limit test pass.

To run the non-rate-limit tests locally, with the normal Compose configuration:

```powershell
$env:API_BASE_URL = "http://localhost:8080"
dotnet test .\tests\KenyaLocation.Api.IntegrationTests\KenyaLocation.Api.IntegrationTests.csproj `
  --filter "FullyQualifiedName!~RateLimitingTests"
```

The rate-limit test should be run against a dedicated API instance configured with a low limit, not against the same low-limit instance used for the full suite.

## Configuration and troubleshooting

### Compose environment variables

| Variable | Purpose | Default/notes |
|---|---|---|
| `POSTGRES_PASSWORD` | Local database password used by all local services | Must be set in the shell before Compose starts. |
| `RATE_LIMIT_PERMIT_LIMIT` | API fixed-window request limit | Defaults to `60`. |
| `RATE_LIMIT_WINDOW_SECONDS` | Rate-limit window length | Defaults to `60`. |
| `API_BASE_URL` | Base URL used by integration tests | Defaults to `http://localhost:8080`. |

ASP.NET Core configuration uses environment variable names such as `ConnectionStrings__Postgres`, `RateLimiting__PermitLimit`, and `RateLimiting__WindowSeconds` inside containers.

### Common issues

- **API container does not start:** inspect `docker compose logs api db ingestion`. The API waits for ingestion to complete successfully.
- **Ingestion exits unsuccessfully:** confirm the archive and GIS files exist, inspect `docker compose logs ingestion`, and check that the database became healthy before ingestion ran.
- **Schema changes do not appear:** existing Docker volumes do not rerun initialization SQL. Use an explicit migration/upgrade procedure; deleting the volume is only suitable for disposable local data.
- **Port conflict:** ensure host ports `8080` and `5432` are available, or intentionally change the Compose port mappings.
- **429 responses during ordinary tests:** check that the API was recreated with the normal rate limit of 60 and that no low-limit environment variables remain in the shell.
- **Stale API after code changes:** rebuild/recreate the service with `docker compose up -d --build api`.
- **PowerShell environment variables persist in the current session:** clear test overrides with `Remove-Item Env:RATE_LIMIT_PERMIT_LIMIT -ErrorAction SilentlyContinue` and `Remove-Item Env:RATE_LIMIT_WINDOW_SECONDS -ErrorAction SilentlyContinue`, then recreate the API.
- **Line-ending warnings on Windows:** Git may report LF/CRLF conversion warnings. They are usually not runtime errors; use the repository's existing Git line-ending policy.

## Azure development environment

Azure resources are provisioned from `deploy/azure/main.bicep`; the accompanying operational notes are in [`deploy/azure/README.md`](deploy/azure/README.md).

| Resource | Current development value / role |
|---|---|
| Resource group | `kenya-location-api-dev-rg` |
| Region | `southafricanorth` (South Africa North) |
| Container Apps environment | `kenya-location-api-dev-env` |
| API Container App | `kenya-location-api-dev` |
| Ingestion Container Apps Job | `kenya-location-api-dev-ingestion` |
| Container Registry | `kenyalocationapidev.azurecr.io` |
| PostgreSQL server | `kenya-location-api-dev-db` |
| Database | `kenya_location` |
| Blob storage account | `kenyalocationdevdata` |
| Blob container | `source-data` |

Container image names used by the development environment:

- `kenya-location-api:dev`
- `kenya-location-ingestion:dev`

### API deployment

The API is an externally accessible Container App listening on port `8080`. Its PostgreSQL connection string is supplied through a Container Apps secret reference, not baked into the image. The API's system-assigned managed identity is used to pull its image from ACR. The current Bicep configuration allows scale-to-zero and sets a maximum of one replica; the first request after idle time may be slower.

### Provisioning warning

Do not run Bicep against the existing resource group as a casual troubleshooting step. Infrastructure deployments can alter or replace persistent resources and database credentials. Review the plan, parameters, firewall/networking, and database recovery implications first. The Bicep file includes environment-specific network configuration; reconcile any manual Azure changes with the code before relying on it to recreate the environment.

## Azure identities and access

The design uses separate identities for separate responsibilities:

| Identity | Type | Required access / purpose |
|---|---|---|
| API Container App identity | System-assigned managed identity | `AcrPull` on the Azure Container Registry so the platform can pull the API image. |
| Ingestion Job identity | User-assigned managed identity, named `kenya-location-api-dev-ingestion-id` | `AcrPull` on ACR and `Storage Blob Data Reader` on the storage account; the job sets `AZURE_CLIENT_ID` so the Azure SDK selects this identity for Blob access. |
| GitHub Actions deployment identity | Federated identity / OIDC | Lets the release workflow authenticate to Azure without storing a long-lived Azure client secret in GitHub. The federated credential subject must exactly match the repository/environment subject configured in GitHub Actions. |

Managed identity access to Blob Storage and ACR is distinct from database authentication. The current database connection uses a credential held as a Container Apps secret; it is not automatically replaced by the managed identities above.

Do not enable the ACR admin user as a shortcut. Verify the correct identity's role assignment, scope, principal ID, and registry/resource name instead.

## Azure data ingestion

Source files are stored in the private `source-data` container. The expected object names are:

```text
gis/kenya-adm1.geojson
gis/kenya-adm2.geojson
gis/kenya-adm3.geojson
places/13308773.zip
```

The ingestion Job is a finite, **manual** workload. It downloads source files using its user-assigned managed identity and imports them into PostgreSQL/PostGIS. It does not continuously monitor Blob Storage.

Operational sequence:

1. Confirm the images are present in ACR and source objects are present in Blob Storage.
2. Confirm the job identity has `AcrPull` and `Storage Blob Data Reader`.
3. Confirm the database connection secret and networking/firewall rules are correct.
4. Start the `kenya-location-api-dev-ingestion` Job manually.
5. Inspect the execution status and logs until it reaches a terminal state.
6. Validate imported counts and representative API endpoints.

Example CLI inspection commands (run after `az login` and selecting the correct subscription):

```powershell
az account show
az containerapp show --name kenya-location-api-dev --resource-group kenya-location-api-dev-rg
az containerapp job show --name kenya-location-api-dev-ingestion --resource-group kenya-location-api-dev-rg
az containerapp job execution list --name kenya-location-api-dev-ingestion --resource-group kenya-location-api-dev-rg --output table
```

Container Apps Job log commands may be in preview and their syntax can vary by Azure CLI extension version. If direct log streaming fails, inspect the Container Apps environment's Log Analytics workspace and query the job/container logs there.

A successful API health check proves the API can respond; it does not by itself prove that a new ingestion execution succeeded. Check the Job execution and validate the data separately.

## CI, releases, and branch workflow

### Continuous integration

Workflow: `.github/workflows/ci.yml`

CI runs on pushes to `develop` and `main`, and on pull requests targeting either branch (the workflow can also be called by another workflow). Its principal steps are:

1. Check out source without persisting the checkout token.
2. Download the urban-centres ZIP required by ingestion.
3. Install .NET 10 and restore the API and integration-test projects.
4. Build the API in Release configuration.
5. Build the API Docker image and ingestion Docker image.
6. Start the Docker Compose stack with the temporary low rate limit.
7. Wait for `/health` and print service logs if setup fails.
8. Run the rate-limit integration test.
9. Recreate the API with the normal 60-request limit.
10. Run the remaining integration tests.
11. Print ingestion logs on failure and tear down the stack and CI database volume.

A green CI run is the main gate before merging. The rate-limit test is deliberately isolated from the ordinary integration tests.

### Branch and release discipline

Use the repository's established promotion path:

1. Create a feature branch from the current `develop`.
2. Make a focused change and run the relevant local checks.
3. Open a pull request into `develop`; wait for CI.
4. Promote `develop` to protected `main` through a pull request.
5. Create a versioned release tag only when a release is intended.
6. The release workflow uses GitHub Actions OIDC to authenticate to Azure and deploy the tagged API image. Confirm the workflow run and smoke-test the deployed endpoints after release.

Do not merge feature work directly into protected `main`, and do not create a release tag merely to document work that does not change the deployed artifact. Check the actual workflow files in `.github/workflows/` for the current release-tag trigger and required GitHub Environment configuration before creating a tag.

### GitHub/Azure deployment setup notes

- Configure the repository/environment variables and secrets required by the release workflow.
- Configure the Azure federated credential to match the exact GitHub repository and Environment subject used by the workflow; a small subject mismatch causes OIDC login to fail.
- The workflow identity needs only the Azure roles required for the intended deployment.
- Keep database administrator credentials in protected secret stores and never print them in CI logs.
- After a deployment, verify `/health`, `/api/v1/meta`, and `/swagger`. Health alone does not verify data freshness.

## Repository layout

```text
.
├── .github/workflows/       # CI and release/deployment workflows
├── database/                # First-initialization SQL scripts
├── data/                    # Local GIS and places source data (may be ignored by Git)
├── deploy/azure/            # Bicep infrastructure and Azure operations notes
├── ingestion/               # Python/GeoPandas import container
├── src/KenyaLocation.Api/   # .NET 10 ASP.NET Core API
├── tests/
│   └── KenyaLocation.Api.IntegrationTests/
├── docker-compose.yml       # Local db, ingestion, and API stack
└── README.md
```

## Data sources and limitations

The project uses public Kenya administrative and place datasets, including:

- [Open Admin Data Kenya](https://github.com/open-admin-data/kenya-administrative-divisions) — administrative data with CC-BY-4.0 attribution requirements.
- [Kenya administrative boundary repository](https://github.com/leoouma/KE_Admin_Boundaries) — boundary files; review the source repository's license and attribution requirements before redistribution.
- [Figshare urban-centres dataset, article 13308773](https://api.figshare.com/v2/articles/13308773) — urban-centres archive.

Preserve source attribution and review the current upstream terms before redistributing derived datasets. Different sources and versions can disagree on boundaries, names, and administrative hierarchy. Electoral constituencies and administrative units are different concepts and must not be conflated. Before using the service for consequential decisions, establish an authoritative source hierarchy, record dataset versions, and reconcile boundaries and hierarchy against approved sources.

## Security and operational notes

- Never commit passwords, PostgreSQL connection strings, GitHub tokens, service-principal secrets, or Azure credentials.
- Keep Blob Storage private and use managed identities for resource-to-resource access.
- Keep ACR admin credentials disabled; use scoped `AcrPull` assignments.
- Treat the development endpoint and development database as non-production.
- Review firewall and public-network settings before production use. The current Bicep is environment-specific and should not be treated as a production-ready network design without review.
- Use explicit database migrations for schema evolution; first-run SQL scripts are not a migration system.
- Validate ingestion execution, data counts, and sample records after every import.
- Add monitoring, alerting, backup/restore drills, data-version tracking, and a production data-governance process before representing this service as production-grade.
