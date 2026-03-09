# Collector.Kernel

Collector.Kernel is a shared library that contains common components and logic used by all Collector services within the Backpack ecosystem.

## Key Components

- **ICollector**: Defines the base interface for all collectors, which are essentially MassTransit consumers for `ArtifactCollectRequest` messages.
- **FileSystem**: Provides shared methods for file system operations, such as creating directories and writing artifact files.
- **StorageExtensions**: Extension methods for configuring and interacting with storage systems like S3.

## Interaction with Other Services

This library is used as a base dependency for all `Collector.*` services. It ensures consistency in how different collectors interact with the system and manage their local file systems and storage.
