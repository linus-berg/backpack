# Service & Module Inventory

This document provides a comprehensive list of all modules, core services, collectors, and processors within the Backpack ecosystem.

## Modules
| Tag | Name | Description |
| :--- | :--- | :--- |
| **Tracker** | Artifact Tracking | Monitoring and update scheduling for external registries. |
| **Processor** | Artifact Processing | Ecosystem-specific metadata extraction and dependency resolution. |
| **Collector** | Artifact Collection | Physical retrieval of artifacts via standard protocols. |

## Core Services
- **`Core.Gateway`**: Central message bus (MassTransit) orchestrating the ingestion lifecycle.
- **`Tracker.Scheduler`**: Quartz.NET-based scheduling for recurring registry synchronization.
- **`Integration.API`**: REST interface for management, monitoring, and OIDC-secured triggers.

## Specialized Collectors
| Service | Protocol / Tool | Description |
| :--- | :--- | :--- |
| **`Collector.Http`** | HTTP/HTTPS | Generic web resource retrieval with existence-based synchronization. |
| **`Collector.Git`** | Git | Full repository synchronization for Git-based artifact ecosystems. |
| **`Collector.Container`** | Skopeo/OCI | Remote registry-to-registry image synchronization (OCI/Layers). |
| **`Collector.DockerArchive`**| Skopeo/TAR | Fetches remote images and saves them as Docker TAR archives. |
| **`Collector.Wget`** | Wget | Recursive website and documentation mirroring. |
| **`Collector.Rsync`** | Rsync | High-speed file synchronization for large mirrors. |
| **`Collector.Router`** | Logic | Internal routing of collection requests to specialized workers. |

## Ecosystem Processors
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

## Utility Services
- **`Backpack.Toolbox`**: CLI utilities for administrative tasks and index management.
- **`Backpack.GitUnpack`**: Dedicated service for decompressing and verifying Git bundles.
- **`Backpack.Tester`**: Automated integration and regression testing suite.
