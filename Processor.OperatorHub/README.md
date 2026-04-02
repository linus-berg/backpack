# Processor.OperatorHub

Processor.OperatorHub is a specialized processor for extracting metadata and dependency information from OperatorHub.io
operators.

## Key Components

- **OperatorHubConsumer**: Implements `IConsumer<ArtifactProcessRequest>`, handling metadata extraction for Kubernetes
  operators.
- **Operator Analysis**: Parses operator manifests and ClusterServiceVersions (CSVs) to identify required images and
  other dependencies.
- **Metadata Extraction**: Gathers information such as operator versions, channels, and maintainer details.

## Interaction with Other Services

- **Core.Gateway**: Receives processing requests from the gateway and returns `ArtifactProcessedRequest` messages with
  extracted metadata.
- **Collector.Router**: Metadata from this processor triggers collection requests via the gateway.
- **OpenTelemetry**: Provides observability and telemetry data for operator processing.
