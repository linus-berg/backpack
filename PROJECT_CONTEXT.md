# Project: Backpack Status & Roadmap

## Active Goals
- **Collector Stability:** Ensuring reliable recursive downloads across all major ecosystem modules (NPM, Docker, NuGet).
- **Metadata Consistency:** Standardizing how metadata is tracked across different Processors via the Core.Gateway.
- **Air-Gap Readiness:** Developing reliable tools for verifying the integrity of local S3/MinIO mirrors.

## Current Service Status
| Module | Processor Status | Collector Status | Notes |
| :--- | :--- | :--- | :--- |
| **NPM** | Stable | Stable (HTTP) | Supports full recursive versioning. |
| **Docker** | Stable | Stable (Skopeo) | Handles image layer mapping and registry copies. |
| **NuGet** | Stable | Stable (HTTP) | Standard .NET package resolution. |
| **Git** | N/A | Stable (Bundle) | Generates incremental `.bundle` files. |
| **HuggingFace** | Experimental | Experimental | Early support for AI model mirroring. |

## Key Technical Assets
- **`Collector.Kernel/FileSystem.cs`:** The unified interface for local/S3 storage.
- **`Integration.API`:** The REST interface for external interaction and status tracking.
- **`Backpack-gui`:** React/Vite dashboard for managing collections.