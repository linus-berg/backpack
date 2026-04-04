# Project: Backpack

## Project Overview
Backpack is a distributed system designed for the recursive, exhaustive collection and tracking of software artifacts and packages (e.g., NPM, PyPi, Docker, NuGet). Its primary purpose is to build comprehensive, offline-ready mirrors for high-security or air-gapped environments.

## Core Architecture & Principles
Backpack follows a microservice-oriented architecture consisting of **Collectors**, **Processors**, and a central **Gateway**. 

- **Functional Definitions**: See **[Glossary of Terms](docs/Glossary.md)**.
- **Service Inventory**: See **[Service & Module Inventory](docs/ServiceInventory.md)**.
- **Exhaustive Collection**: By default, Backpack recursively identifies and collects every version and dependency version until the entire tree is mirrored locally.
- **Backend-Only Focus**: Backpack is a collection and synchronization engine. It does not provide a public-facing repository server or "upload" artifacts to external registries.

## Technical Standards
- **Dotnet (Backend):** 
  - Use explicit types (e.g., `List<string>`) instead of `var`.
  - Adhere to the established microservice pattern (Gateway -> Processor -> Collector).
  - Leverage `Collector.Kernel` and its `FileSystem` abstraction for all storage operations.
- **TypeScript/React (Frontend):**
  - Always use the `useUser` hook for role and permission checks.
  - Use Styled Components for all UI elements; avoid inline CSS or external utility-first frameworks unless specified.
  - Follow the existing JSDoc documentation style for all new functions and components.
- **Storage:** Artifacts are stored in a unified S3 structure: `s3://<bucket>/<module>/<location>`.

## Operational Requirements
- A full deployment requires: RabbitMQ (Messaging), MinIO (S3 Storage), MongoDB (Metadata), Redis (Caching), and an OIDC Provider (Authentication).
