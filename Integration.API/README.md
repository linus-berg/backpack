# Integration.API

Integration.API is the primary external entry point for the Backpack system. It provides a RESTful API for clients to interact with the artifact repository.

## Key Components

- **ArtifactController**: Exposes endpoints for:
  - Ingesting artifacts (`POST /api/artifact/ingest`)
  - Querying artifact status and metadata
  - Listing available artifacts
- **Authentication**: Uses Keycloak for secure access control.
- **MassTransit Integration**: Publishes `ArtifactIngestRequest` messages to the message bus (`Core.Gateway`).

## Interaction with Other Services

- **Core.Gateway**: Publishes ingestion requests to be processed by the gateway.
- **Core.Services**: Uses the shared services for database and cache interactions.
- **Keycloak**: Validates authentication tokens for secure endpoints.
- **OpenTelemetry**: Provides observability and telemetry data.
