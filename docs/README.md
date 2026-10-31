# Backpack Documentation

## Understanding Backpack

- [Architecture Diagrams](ArchitectureDiagrams.md): components, queues and the life of an
  artifact.
- [Service Inventory](ServiceInventory.md): every service, its queues, and which
  collectors each processor needs.
- [Storage Architecture](../STORAGE.md): how collected files are named in the bucket, and
  delta links.
- [Glossary](Glossary.md): the terms used in the code and these documents.
- [Type Reference](TypeReference.md): artifacts, messages and stored documents.

## Running Backpack

- [Development Setup](DevelopmentSetup.md): a local stack for working on Backpack.
- [Deployment Guide](DeploymentGuide.md): infrastructure, images, Helm and Compose,
  sign-in, security notes.
- [Configuration](Configuration.md): every environment variable, and processor
  definitions.
- [Troubleshooting](Troubleshooting.md): following an artifact through the system, and
  common problems.
- [Example stack](../example/README.md): a single-machine Compose deployment for
  evaluation.

## Extending Backpack

- [Processor Development](ProcessorDevelopment.md): adding an ecosystem with the .NET
  template.
- [Raw Integration](RawIntegration.md): processors and collectors in other languages.

Each service also has a README in its directory (for example
[`Collector.Git/README.md`](../Collector.Git/README.md)) with its queues, settings and
behaviour in detail. The API's endpoints are listed in
[`Integration.API/README.md`](../Integration.API/README.md).
