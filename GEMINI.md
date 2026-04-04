# Project: Backpack

## Project Overview
Backpack is a distributed system designed for the recursive, exhaustive collection and tracking of software artifacts and packages (e.g., NPM, PyPi, Docker, NuGet). Its primary purpose is to build comprehensive, offline-ready mirrors for high-security or air-gapped environments.

## Core Architecture & Principles
- **Collectors (Artifact Retrieval):** Responsible for the physical download of resources using standard protocols (HTTP, Git, Skopeo/Docker, Rsync, Wget).
- **Processors (Metadata & Logic):** Ecosystem-specific services that resolve dependency trees, extract metadata, and identify which specific files need to be collected.
- **Core.Gateway:** The central message bus (using MassTransit) that routes requests between the API, Processors, and Collectors.
- **Exhaustive Collection Logic:** By default, Backpack is designed to be exhaustive. It recursively identifies and collects every version and every dependency version of a requested artifact until the entire tree is mirrored locally.
- **Backend-Only Focus:** Backpack is a collection and synchronization engine. It manages storage (S3/MinIO) and tracking (MongoDB/Redis) but does not provide a public-facing repository server or "upload" artifacts to external registries.

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
