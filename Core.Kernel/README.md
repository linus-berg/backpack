# Core.Kernel

`Core.Kernel` is the foundational library for the Backpack ecosystem. It contains the shared message definitions,
models, constants, and extension methods used by all services.

## Role

The Kernel acts as the single source of truth for communication protocols, data models, and system-wide configuration.
It ensures consistency across all microservices and libraries.

## Key Components

- **Messages**: Defines the standard MassTransit messages for artifact ingestion, processing, routing, and collection (
  e.g., `ArtifactIngestRequest`, `ArtifactProcessRequest`, `ArtifactProcessedRequest`).
- **Models**: Defines the core data structures used in the system, such as `Artifact`, `ArtifactVersion`, and
  `ArtifactDependency`.
- **Constants**: Contains system-wide constants and configuration variable names (`CoreVariables`).
- **Registrations**: Provides utility methods for registering services, logging, and telemetry consistently across
  different host types (`ModuleRegistration`, `RegistrationUtils`).
- **Extensions**: Includes helper methods for configuring MassTransit, RabbitMQ, and other common libraries.
- **Bin**: Utilities for executing external processes via `CliWrap`.

## Interactions

- **Universal Dependency**: Almost every project in the Backpack solution references `Core.Kernel`.
- **External Integration**: Defines the interfaces for Processors (`IProcessor`) and Collectors (`ICollector`) that
  other modules must implement.

## Shared Functionality

`Core.Kernel` provides the standard setup for:

- **Logging**: Unified logging configuration for all modules.
- **Telemetry**: OpenTelemetry integration for tracing and monitoring.
- **Messaging**: MassTransit configuration and RabbitMQ setup.
- **Configuration**: Standardized access to environment variables.
