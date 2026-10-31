<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="images/backpack-logo-dark.svg">
    <img src="images/backpack-logo.svg" alt="Backpack logo" width="160">
  </picture>
</p>

# Backpack: Distributed Artifact Synchronization & Mirroring Engine

Backpack is a high-performance, distributed system for people whose security teams have serious trust issues. It is engineered for those building in the quiet, isolated comfort of air-gapped bunkers who still need their 4GB of `node_modules` despite having zero bytes of external connectivity.

At its core, Backpack is a **one-way artifact collection and tracking engine**. It doesn't host repositories or serve files to your developers; instead, it acts as a tireless background harvester. It recursively resolves dependency trees for entire ecosystems (npm, NuGet, PyPI, container images, Helm, Terraform, Hugging Face, Git and more), fetches every available version, and persists them into S3-compatible storage for later transfer to your isolated environment.

Think of it as a professional-grade, recursively-obsessive digital hoarder: Backpack siphons every version and every dependency from the public internet and stuffs them into your private storage. It harvests all, presents nothing, and ensures that when the internet inevitably lets you down (or your firewall is just that good), your codebase is already stocked for the long-term survival of your builds.

## How it works

1. You add an artifact (`lodash`, `docker.io/library/alpine`, `git://github.com/owner/repo`, ...) in the
   [GUI](https://github.com/linus-berg/backpack-gui) or through the API.
2. The ecosystem's **processor** asks the upstream registry for every version, the files that make up each
   version, and the artifact's direct dependencies.
3. **Core.Gateway** stores what changed in MongoDB, hands the files to **Collector.Router**, and sends every
   dependency to its processor in turn, until the whole dependency tree is known.
4. **Collectors** download each file that isn't in the bucket yet, store it under
   `<module>/<host><path>`, and record it in a daily **delta** folder, so you know exactly what to carry
   across the air gap.
5. **Tracker.Scheduler** re-tracks ecosystems on a schedule to pick up new versions.

Everything talks through RabbitMQ, so each ecosystem and protocol is its own small service that you deploy
only if you need it.

## Quick start

- **Try it**: [`example/`](example/README.md) runs a complete, single-machine stack with Docker Compose.
- **Develop**: start the infrastructure from [`Compose/`](Compose/README.md) and run the services from your
  IDE; see [Development Setup](docs/DevelopmentSetup.md).
- **Deploy**: Kubernetes via the [Helm chart](https://github.com/linus-berg/helm-charts); see the
  [Deployment Guide](docs/DeploymentGuide.md).

## 📖 Documentation

### Understanding Backpack
- **[Architecture Diagrams](docs/ArchitectureDiagrams.md)**: components, queues and the life of an artifact.
- **[Service Inventory](docs/ServiceInventory.md)**: every processor and collector, with its queues and what it needs.
- **[Storage Architecture](STORAGE.md)**: how collected files are laid out in the bucket, and delta links.
- **[Glossary](docs/Glossary.md)**: the terms used in the code and documentation.
- **[Type Reference](docs/TypeReference.md)**: artifacts, messages and stored documents.

### Running Backpack
- **[Deployment Guide](docs/DeploymentGuide.md)**: infrastructure, images, Helm and Compose, sign-in, security notes.
- **[Configuration](docs/Configuration.md)**: every environment variable, and processor definitions.
- **[Development Setup](docs/DevelopmentSetup.md)**: a local stack for working on Backpack.
- **[Troubleshooting](docs/Troubleshooting.md)**: following an artifact through the system, and common problems.

### Extending Backpack
- **[Processor Development](docs/ProcessorDevelopment.md)**: adding an ecosystem with the .NET template.
- **[Raw Integration](docs/RawIntegration.md)**: processors and collectors in other languages.
- **[Integration.API](Integration.API/README.md)**: REST endpoints, roles and API keys.
- **[Contributor standards](GEMINI.md)**: conventions for code in this repository.

Every service has its own README with its queues, settings and behaviour.

## 🛠 Technical stack

- **Runtime**: .NET 8 (C#), one container image per service.
- **Messaging**: MassTransit on RabbitMQ.
- **Persistence**: S3-compatible object storage (artifacts), MongoDB (artifact metadata, settings, events),
  Redis (per-ingest dependency tracking).
- **Observability**: OpenTelemetry traces, metrics and logs over OTLP.
- **Security**: any OIDC provider, an `Administrator` role, API keys for automation, optional approval of
  requests per ecosystem.

## 🚀 Infrastructure requirements

- **RabbitMQ** with the management and delayed-message-exchange plugins (the `masstransit/rabbitmq` image
  has both).
- **S3-compatible storage**: MinIO, SeaweedFS, Ceph, AWS S3, ...
- **MongoDB** and **Redis**.
- **An OIDC provider**, e.g. Keycloak.
- **A container registry**, only if you mirror container images.
