# Collector.Router

Collector.Router is a specialized service within the Backpack ecosystem responsible for routing collection requests to the appropriate collector based on the artifact type.

## Key Components

- **Router**: Implements `IConsumer<ArtifactRouteRequest>`, handling requests to determine which collector is responsible for a given artifact and its versions.
- **Filtering**: Applies optional artifact filters (e.g., semantic version ranges or regular expressions) to determine which specific versions should be collected.
- **Routing Logic**: Maps artifact metadata to specific collector modules (e.g., `git`, `http`, `container`) and emits `ArtifactCollectRequest` messages.

## Interaction with Other Services

- **Core.Gateway**: Receives `ArtifactRouteRequest` messages from the gateway's `ProcessedConsumer`.
- **Collectors**: Publishes `ArtifactCollectRequest` messages to specific collector queues.
- **OpenTelemetry**: Provides observability and telemetry data for routing and filtering operations.
