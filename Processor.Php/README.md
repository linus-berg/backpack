# Processor.Php

Processor.Php is responsible for extracting metadata and dependency information from PHP packages, primarily from the
Packagist ecosystem.

## Key Components

- **PhpConsumer**: Implements `IConsumer<ArtifactProcessRequest>`, handling metadata extraction for PHP packages.
- **Dependency Resolution**: Analyzes `composer.json` and other metadata files to identify package versions and their
  dependencies.
- **Metadata Extraction**: Gathers details such as package name, description, version, and maintainer information.

## Interaction with Other Services

- **Core.Gateway**: Receives processing requests from the gateway and returns `ArtifactProcessedRequest` messages with
  extracted metadata.
- **Collector.Router**: Metadata from this processor triggers collection requests for PHP package source code and
  artifacts.
- **OpenTelemetry**: Provides observability and telemetry data for PHP package processing.
