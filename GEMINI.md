# Project: Backpack

## Project Overview
Backpack is a distributed system designed for the recursive, exhaustive collection and tracking of software artifacts and packages (e.g., NPM, PyPi, Docker, NuGet). Its primary purpose is to build comprehensive, offline-ready mirrors for high-security or air-gapped environments.

## Core Architecture & Principles
Backpack follows a microservice-oriented architecture consisting of **Processors**, **Collectors**, a central **Gateway**, the **Collector.Router**, the **Integration.API** and the **Tracker.Scheduler**, all connected through RabbitMQ (MassTransit). See **[Architecture Diagrams](docs/ArchitectureDiagrams.md)**.

- **Functional Definitions**: See **[Glossary of Terms](docs/Glossary.md)**.
- **Service Inventory**: See **[Service & Module Inventory](docs/ServiceInventory.md)**.
- **Exhaustive Collection**: By default, Backpack recursively identifies and collects every version and dependency version until the entire tree is mirrored locally.
- **Backend-Only Focus**: Backpack is a collection and synchronization engine. It does not provide a public-facing repository server or "upload" artifacts to external registries.

## Technical Standards
- **Dotnet (Backend):** 
  - Use explicit types (e.g., `List<string>`) instead of `var`.
  - Adhere to the established microservice pattern (Gateway -> Processor -> Gateway -> Router -> Collector). Processors only read upstream metadata and report direct dependencies; the Gateway does the recursion; Collectors only download and store.
  - Leverage `Collector.Kernel` and its `FileSystem` abstraction for all storage operations.
- **TypeScript/React (Frontend, [backpack-gui](https://github.com/linus-berg/backpack-gui)):**
  - Always use the `useUser` hook for role and permission checks.
  - Use Styled Components for all UI elements; avoid inline CSS or external utility-first frameworks unless specified.
  - Follow the existing JSDoc documentation style for all new functions and components.
- **Storage:** Artifacts are stored in a unified S3 structure: `s3://<bucket>/<module>/<host><path>`, derived from the file's URL by `Collector.Kernel.FileSystem`. See **[Storage Architecture](STORAGE.md)**.
- **Documentation:** Every service has a README describing its queues, settings and behaviour. Keep it, and the documents in `docs/`, in sync with the code when you change behaviour, queues or environment variables.

## Operational Requirements
- A full deployment requires: RabbitMQ (Messaging), S3-compatible storage (Artifacts), MongoDB (Metadata), Redis (per-ingest dependency tracking), and an OIDC Provider (Authentication). See **[Deployment Guide](docs/DeploymentGuide.md)** and **[Configuration](docs/Configuration.md)**.
