# Collector.DockerArchive

Collector.DockerArchive is responsible for collecting container images stored in local Docker archive (`.tar`) files.

## Key Components

- **DockerArchiveConsumer**: Implements `IConsumer<ArtifactCollectRequest>`, handling collection requests for image archives.
- **Archive Processing**: Extracts image layers and metadata from the `.tar` file.
- **Image Conversion**: Converts Docker archive formats to OCI-compliant artifacts as needed.

## Interaction with Other Services

- **Core.Gateway**: Receives collection requests from the gateway (via `Collector.Router`).
- **OpenTelemetry**: Provides observability and telemetry data for image collection processes.
