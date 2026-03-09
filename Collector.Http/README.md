# Collector.Http

Collector.Http is a versatile collector for retrieving artifacts over HTTP and HTTPS protocols.

## Key Components

- **HttpConsumer**: Implements `IConsumer<ArtifactCollectRequest>`, handling collection requests for generic HTTP/HTTPS resources.
- **Delta Support**: Supports incremental downloads (deltas) based on HTTP headers such as `ETag` and `Last-Modified`.
- **Download Management**: Handles retries, timeouts, and streaming of large files.

## Interaction with Other Services

- **Core.Gateway**: Receives collection requests from the gateway (via `Collector.Router`).
- **OpenTelemetry**: Provides observability and telemetry data for HTTP collection processes.
