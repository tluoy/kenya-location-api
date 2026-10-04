# Azure Deployment

This directory contains deployment configuration for the Kenya Location API.

## Target Azure topology

- Azure Container Apps — API
- Azure Container Apps Job — one-shot data ingestion
- Azure Database for PostgreSQL Flexible Server — PostGIS database

## Deployment principles

- The API is a long-running containerized service.
- Data ingestion is a finite workload and must not run as a continuously running service.
- PostgreSQL/PostGIS is persistent infrastructure and is not deployed as part of the application container stack.
- Production secrets must be supplied through Azure configuration/secrets rather than committed to Git.
- Deployment changes are developed on a feature branch and promoted through develop and main.

## Current status

Azure infrastructure has not yet been provisioned.

The files in this directory will be added incrementally as the deployment design is implemented.
