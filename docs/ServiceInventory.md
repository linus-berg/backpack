# Service & Module Inventory

This document provides a comprehensive list of all modules, core services, collectors, and processors within the Backpack ecosystem.

## 🟢 Core Modules

### **Tracker (Tracker.Scheduler)**
Responsible for monitoring and update scheduling for external registries using Quartz.NET. It defines when the system should check for new artifacts.

### **Processor**
Ecosystem-specific logic units responsible for metadata extraction and dependency resolution. They handle the logic of "how to find" what needs to be mirrored.

### **Collector**
Protocol-specific workers responsible for the physical retrieval of artifacts via standard protocols (HTTP, Git, etc.). They handle the logic of "how to fetch" individual files.

---

## 🔵 Core System Services

- **`Core.Gateway`**: The central orchestrator. It manages the state machine for artifact ingestion, routing requests between Processors and Collectors, and ensuring metadata consistency in MongoDB.
- **`Integration.API`**: A RESTful gateway for system management, monitoring, and OIDC-secured triggers.
- **`Tracker.Scheduler`**: Quartz.NET-based scheduling for recurring registry synchronization.

---

## 🟠 Specialized Collectors

| Service | Protocol / Tool | Description |
| :--- | :--- | :--- |
| **`Collector.Http`** | HTTP/HTTPS | Generic web resource retrieval with existence-based synchronization. |
| **`Collector.Git`** | Git | Full repository synchronization for Git-based artifact ecosystems. |
| **`Collector.Container`** | Skopeo/OCI | Remote registry-to-registry image synchronization (OCI/Layers). |
| **`Collector.DockerArchive`**| Skopeo/TAR | Fetches remote images and saves them as Docker TAR archives for archival use. |
| **`Collector.Wget`** | Wget | Recursive website and documentation mirroring. |
| **`Collector.Rsync`** | Rsync | High-speed file synchronization for large mirrors. |
| **`Collector.Router`** | Internal Logic | Routes collection requests to the specialized workers above. |

---

## 🟡 Ecosystem Processors

Processors consume an `ArtifactProcessRequest`, resolve the dependency manifest, and emit `ArtifactRouteRequest` messages for individual files.

| Service | Ecosystem | Functionality |
| :--- | :--- | :--- |
| **`Processor.Npm`** | Node.js (NPM) | Recursive dependency resolution for the NPM registry. |
| **`Processor.Pypi`** | Python (PyPI) | Metadata extraction and file identification for PyPI. |
| **`Processor.Nuget`** | .NET (NuGet) | Dependency tree resolution for NuGet. |
| **`Processor.Maven`** | Java (Maven) | Metadata extraction for Maven repositories. |
| **`Processor.Container`** | OCI/Docker | Metadata extraction for container images. |
| **`Processor.Helm`** | K8s Helm | Manifest resolution for Helm charts. |
| **`Processor.Terraform`** | Terraform | Module dependency tracking for Terraform. |
| **`Processor.OperatorHub`** | K8s Operators | Version tracking for OperatorHub.io. |
| **`Processor.HuggingFace`** | AI/ML | Support for model and dataset mirroring. |
| **`Processor.Github.Releases`**| GitHub | Artifact identification from GitHub release tags. |
| **`Processor.Jetbrains.*`** | IDE/Plugins | Identification of binaries for the JetBrains ecosystem. |

---

## 🟣 Utility & Supporting Services

- **`Backpack.Toolbox`**: CLI utilities for administrative tasks, index management, and data generation.
- **`Backpack.GitUnpack`**: Service for decompressing and verifying legacy Git bundles.
- **`Backpack.Tester`**: Comprehensive automated integration and regression testing suite.
