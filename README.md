<p align="center">
  <img src="images/header-image.png" alt="Backpack Header" width="100%">
</p>

# Backpack: Distributed Artifact Synchronization & Mirroring Engine

Backpack is a high-performance, distributed system for people whose security teams have serious trust issues. It is engineered for those building in the quiet, isolated comfort of air-gapped bunkers who still need their 4GB of `node_modules` despite having zero bytes of external connectivity.

At its core, Backpack is a **one-way artifact collection and tracking engine**. It doesn't host repositories or serve files to your developers; instead, it acts as a tireless background harvester. It recursively resolves dependency trees for entire ecosystems (NPM, Docker, NuGet, etc.), fetches every available version, and persists them into a unified S3-compatible storage layer for later transfer to your isolated environment. 

Think of it as a professional-grade, recursively-obsessive digital hoarder: Backpack siphons every version and every dependency from the public internet and stuffs them into your private storage. It harvests all, presents nothing, and ensures that when the internet inevitably lets you down (or your firewall is just that good), your codebase is already stocked for the long-term survival of your builds.

## 📖 Documentation Index

To understand the system's design and operational details, refer to the following guides:

### Core Architecture & Strategy
- **[System Philosophy & Standards](GEMINI.md)**: Core mandates, exhaustive collection logic, and architectural principles.
- **[Service & Module Inventory](docs/ServiceInventory.md)**: Detailed inventory of all core modules, processors, and collectors.
- **[Architecture & Diagrams](docs/ArchitectureDiagrams.md)**: Visual representation of message propagation and service interaction.
- **[Storage Architecture](STORAGE.md)**: Detailed breakdown of the S3-hierarchical structure and `Collector.Kernel` implementation.
- **[Glossary of Terms](docs/Glossary.md)**: Standardized definitions for Backpack architecture, roles, and components.
- **[Type Reference](docs/TypeReference.md)**: Technical schemas for internal messages and metadata structures.

### Deployment & Configuration
- **[Configuration Guide](docs/Configuration.md)**: Environment variables, service endpoints, and infrastructure settings.
- **[Deployment Guide](docs/DeploymentGuide.md)**: Full solution deployment (Docker Compose and Kubernetes/Helm).
- **[Development Setup](docs/DevelopmentSetup.md)**: Detailed technical instructions for local development environments.
- **[Troubleshooting](docs/Troubleshooting.md)**: Common failure patterns and resolution steps.

### Extension & Integration
- **[Ecosystem Development Guide](docs/EcosystemDevelopment.md)**: Complete guide to implementing custom package managers and scaffolded processors.
- **[Raw Integration API](docs/RawIntegration.md)**: Documentation for interacting directly with the `Integration.API`.

---

## 🏗 System Architecture Overview

Backpack follows a **microservice-oriented architecture** centered around a decoupled message-passing pattern using **MassTransit** and **RabbitMQ**.

For a complete list of all services, see the **[Service & Module Inventory](docs/ServiceInventory.md)**.

### 1. Core Services
The central orchestrators of the ingestion lifecycle, managing state and routing through **MongoDB**.

### 2. Artifact Processors
Ecosystem-specific logic units (NPM, PyPi, Docker, etc.) that resolve dependency manifests and identify required files.

### 3. Artifact Collectors
Protocol-specific workers (HTTP, Git, Skopeo, etc.) that perform the physical retrieval and persistence to **S3-compatible storage**.

## 🛠 Technical Stack

- **Runtime**: .NET 8.0 (C#)
- **Messaging**: MassTransit with RabbitMQ transport.
- **Persistence**: S3-compatible Object Storage (Artifacts), MongoDB (Metadata), Redis (Distributed Caching).
- **Observability**: Fully instrumented with **OpenTelemetry** (Tracing, Metrics, Logs).
- **Security**: Generic OIDC provider support with RBAC (Role-Based Access Control).

## 🚀 Infrastructure Requirements

To run a full production-ready suite of Backpack services, the following infrastructure sidecars are required:
- **RabbitMQ**: Message brokering.
- **MinIO/S3**: Artifact persistence.
- **MongoDB**: Metadata and tracking state.
- **Redis**: Rate limiting and caching.
- **OIDC Provider**: Authentication (e.g., Keycloak).

---
*Note: This project is optimized for horizontal scalability and high-concurrency ingestion workloads.*
