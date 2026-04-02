# Core.Gateway

`Core.Gateway` is the central message hub for the Backpack system. It is a worker service that
uses [MassTransit](https://masstransit.io/) with RabbitMQ to coordinate communication between the various microservices
in the ecosystem.

## Role

The Gateway acts as the orchestrator for artifact processing and collection. It receives requests from the API or
Scheduler, routes them to the appropriate Processors, handles the results, and triggers collection via the Router and
Collectors.

## Key Components

- **IngestConsumer**: Handles `ArtifactIngestRequest` messages. It initiates the processing of new artifacts by
  interacting with the `IArtifactService`.
- **ProcessedConsumer**: Handles `ArtifactProcessedRequest` messages sent by Processors. It updates the artifact
  metadata in the database and determines if further actions (like collecting files or processing dependencies) are
  needed.
- **ProcessedRawConsumer**: Similar to `ProcessedConsumer` but for raw artifact data processing.
- **ProcessingFaultConsumer**: Handles faults that occur during message processing, ensuring system resilience.
- **Worker**: The background service that keeps the Gateway running and listening for messages.

## Interactions

- **Inbound**:
    - Receives `ArtifactIngestRequest` from `Integration.API` or `Tracker.Scheduler`.
    - Receives `ArtifactProcessedRequest` from various **Processors** (e.g., `Processor.Npm`, `Processor.Pypi`).
- **Outbound**:
    - Sends `ArtifactProcessRequest` to **Processors**.
    - Sends `ArtifactRouteRequest` to `Collector.Router`.
    - Sends `ArtifactCollectRequest` to various **Collectors** (via `IArtifactService`).
- **Data Persistence**: Uses `ICoreDatabase` (implemented via MongoDB) for storing artifact metadata and `ICoreCache` (
  implemented via Redis) for tracking processing state.

## Configuration

The Gateway requires connection strings for RabbitMQ, MongoDB, and Redis, which are typically provided via environment
variables as defined in the root `README.md`.
