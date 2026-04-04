<p align="center">
  <img src="images/header-image.png" alt="Backpack Header" width="100%">
</p>

# Backpack: Distributed Artifact Synchronization & Mirroring Engine

Backpack is a high-performance, distributed system designed for the **greedy, recursive ingestion** of software artifacts and package ecosystems. It is engineered to facilitate the creation of comprehensive, offline-ready mirrors for **air-gapped** or high-security environments where direct access to public registries is prohibited.

## 📖 Documentation Index

To understand the system's design and operational details, refer to the following guides:

### Core Architecture & Strategy
- **[System Philosophy & Standards](GEMINI.md)**: Core mandates, greedy collection logic, and architectural principles.
- **[Architecture & Diagrams](docs/ArchitectureDiagrams.md)**: Visual representation of message propagation and service interaction.
- **[Storage Architecture](STORAGE.md)**: Detailed breakdown of the S3-hierarchical structure and `Collector.Kernel` implementation.
- **[Project Roadmap & Status](PROJECT_CONTEXT.md)**: Current state of ecosystem modules and future development goals.
- **[Type Reference](docs/TypeReference.md)**: Technical schemas for internal messages and metadata structures.

### Deployment & Configuration
- **[Configuration Guide](docs/Configuration.md)**: Environment variables, service endpoints, and infrastructure settings.
- **[Deployment Guide](docs/DeploymentGuide.md)**: Full solution deployment (Docker Compose and Kubernetes/Helm).
- **[Development Setup](Development.md)**: Instructions for local development (Docker Compose, MinIO, OIDC setup).
- **[Troubleshooting](docs/Troubleshooting.md)**: Common failure patterns and resolution steps.

### Extension & Integration
- **[Ecosystem Integration](docs/EcosystemIntegration.md)**: How to implement new Processors and integrate custom package managers.
- **[Raw Integration API](docs/RawIntegration.md)**: Documentation for interacting directly with the `Integration.API`.

## Core Architectural Principles

The system follows a **microservice-oriented architecture** centered around a decoupled message-passing pattern.

*   **Distributed Orchestration:** Leveraging **MassTransit** and **RabbitMQ** for reliable message delivery and service discovery.
*   **Greedy Ingestion Logic:** By default, the system performs exhaustive dependency graph resolution. It recursively identifies and collects every version and every dependency version of a target artifact until the entire tree is mirrored locally.
*   **Storage Abstraction:** All persistence operations are handled via a unified `FileSystem` kernel, abstracting S3-compatible object storage (MinIO/AWS S3) for long-term retention.
*   **Ecosystem-Agnostic Core:** The logic for "how to find" (Processors) is separated from "how to fetch" (Collectors), allowing for rapid integration of new package managers.

## System Components

### 1. Core Services
- **`Core.Gateway`**: The central orchestrator. It manages the state machine for artifact ingestion, routing requests between Processors and Collectors, and ensuring metadata consistency in **MongoDB**.
- **`Integration.API`**: A RESTful gateway for system management and manual ingestion triggers, secured via **OIDC** (OpenID Connect).
- **`Tracker.Scheduler`**: A **Quartz.NET**-driven scheduling engine that monitors external registries for updates and triggers periodic synchronization jobs.

### 2. Artifact Processors
Processors are ecosystem-specific logic units responsible for metadata extraction and dependency resolution.
- **Ecosystems**: NPM, PyPi, NuGet, Maven, Helm, Terraform, HuggingFace, Container Images (OCI), and JetBrains IDEs.
- **Function**: They consume an `ArtifactProcessRequest`, resolve the dependency manifest, and emit `ArtifactRouteRequest` messages for individual files.

### 3. Artifact Collectors
Collectors are protocol-specific workers responsible for the physical retrieval of resources.
- **Protocols**: **HTTP/HTTPS**, **Git** (generating incremental `.bundle` files), **Skopeo** (for OCI/Docker registry-to-registry copies), **Rsync**, and **Wget**.
- **Efficiency**: Implements daily delta logic, utilizing `ETags` and `Last-Modified` headers to minimize egress costs and bandwidth usage.

## 🏗 Service & Module Inventory

### Modules
| Tag | Name | Description |
| :--- | :--- | :--- |
| **Tracker** | Artifact Tracking | Monitoring and update scheduling for external registries. |
| **Processor** | Artifact Processing | Ecosystem-specific metadata extraction and dependency resolution. |
| **Collector** | Artifact Collection | Physical retrieval of artifacts via standard protocols. |

### Core Services
- **`Core.Gateway`**: Central message bus (MassTransit) orchestrating the ingestion lifecycle.
- **`Tracker.Scheduler`**: Quartz.NET-based scheduling for recurring registry synchronization.
- **`Integration.API`**: REST interface for management, monitoring, and OIDC-secured triggers.

### Specialized Collectors
| Service | Protocol / Tool | Description |
| :--- | :--- | :--- |
| **`Collector.Http`** | HTTP/HTTPS | Generic web resource retrieval with ETag/Delta support. |
| **`Collector.Git`** | Git | Incremental Git Bundle generation for repository mirroring. |
| **`Collector.Container`** | Skopeo/OCI | Registry-to-registry container image synchronization. |
| **`Collector.Wget`** | Wget | Recursive website and documentation mirroring. |
| **`Collector.Rsync`** | Rsync | High-speed file synchronization for large mirrors. |
| **`Collector.Router`** | Logic | Internal routing of collection requests to specialized workers. |

### Ecosystem Processors
| Service | Ecosystem |
| :--- | :--- |
| **`Processor.Npm`** | Node.js (NPM) packages. |
| **`Processor.Pypi`** | Python (PyPI) packages. |
| **`Processor.Nuget`** | .NET (NuGet) packages. |
| **`Processor.Maven`** | Java (Maven) packages. |
| **`Processor.Container`** | OCI/Docker container images. |
| **`Processor.Helm`** | Kubernetes Helm charts. |
| **`Processor.Terraform`** | Terraform modules. |
| **`Processor.OperatorHub`** | Kubernetes Operators. |
| **`Processor.HuggingFace`** | AI/ML models and datasets. |
| **`Processor.Github.Releases`** | GitHub Release artifacts. |
| **`Processor.Jetbrains.*`** | IDE binaries and plugin ecosystems. |

### Utility Services
- **`Backpack.Toolbox`**: CLI utilities for administrative tasks and index management.
- **`Backpack.GitUnpack`**: Dedicated service for decompressing and verifying Git bundles.
- **`Backpack.Tester`**: Automated integration and regression testing suite.

## Technical Stack

- **Runtime**: .NET 8.0 (C#)
- **Messaging**: MassTransit with RabbitMQ transport.
- **Persistence**: S3-compatible Object Storage (Artifacts), MongoDB (Metadata), Redis (Distributed Caching).
- **Observability**: Fully instrumented with **OpenTelemetry** (Tracing, Metrics, Logs).
- **Security**: Generic OIDC provider support with RBAC (Role-Based Access Control).

## Infrastructure Requirements

To run a full production-ready suite of Backpack services, the following infrastructure sidecars are required:
- **RabbitMQ**: Message brokering.
- **MinIO/S3**: Artifact persistence.
- **MongoDB**: Metadata and tracking state.
- **Redis**: Rate limiting and caching.
- **OIDC Provider**: Authentication (e.g., Keycloak).

---
*Note: This project is optimized for horizontal scalability and high-concurrency ingestion workloads.*
