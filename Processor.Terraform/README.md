# Processor.Terraform

Processor.Terraform is a specialized processor for extracting metadata and dependency information from Terraform
modules.

## Key Components

- **TerraformConsumer**: Implements `IConsumer<ArtifactProcessRequest>`, handling metadata extraction for Terraform
  modules.
- **Module Analysis**: Parses Terraform module configurations to identify provider dependencies and module versions.
- **Metadata Extraction**: Gathers information such as module name, version, and maintainer details.

## Interaction with Other Services

- **Core.Gateway**: Receives processing requests from the gateway and returns `ArtifactProcessedRequest` messages with
  extracted metadata.
- **Collector.Router**: Metadata from this processor triggers collection requests for Terraform module source code.
- **OpenTelemetry**: Provides observability and telemetry data for Terraform module processing.
