# Backpack Storage Architecture

This document describes the unified storage strategy and hierarchical structure used by Backpack to persist artifacts in S3-compatible object storage.

---

## 🏗 Unified Storage Structure

All collectors utilize a standardized path resolution logic managed by the `Collector.Kernel.FileSystem` abstraction. This ensures consistency across different ecosystems and simplifies downstream consumption.

The root of the S3 bucket follows a strict hierarchical pattern:
`s3://<bucket-name>/<module>/<location>`

### Path Components
- **`<module>`**: The ecosystem or processor type (e.g., `npm`, `nuget`, `git`, `docker-archive`).
- **`<location>`**: The unique identifier or relative path for the artifact within that ecosystem.

### Persistence Examples
- **NPM**: `s3://backpack/npm/registry.npmjs.org/lodash/lodash-4.17.21.tgz`
- **NuGet**: `s3://backpack/nuget/api.nuget.org/newtonsoft.json/13.0.1/newtonsoft.json.13.0.1.nupkg`
- **Docker (TAR)**: `s3://backpack/docker-archive/docker.io/library/alpine/3.18.tar`
- **Git**: `s3://backpack/git/github.com/dotnet/runtime.git`

---

## 📁 The Storage Kernel (`Collector.Kernel`)

The `FileSystem.cs` in the `Collector.Kernel` project provides the primary interface for all persistence operations. It abstracts the underlying storage provider and ensures technical mandates are met.

### Key Features
- **Atomic Persistence**: Ensures that artifacts are fully downloaded and verified in temporary storage before being committed to S3.
- **Provider Abstraction**: Transparently handles switching between local disk (for transient processing) and object storage (for permanent retention).
- **Delta Tracking**: Integrates with the internal state machine to create "delta-links" for newly ingested artifacts.

---

## ⚙️ Operation Modes

### 1. Existence-Based Synchronization
By default, Backpack collectors perform an existence check in the primary storage before initiating a download. If the artifact (at its specific version/path) already exists, the retrieval is bypassed unless a "force" ingestion is triggered.

### 2. Delta Persistence
When the `BP_COLLECTOR_HTTP_DELTA` (or equivalent) setting is enabled, the system generates a symlink-like metadata entry for every new artifact ingestion. This allows for the identification of incremental changes between sync cycles.

---

For a detailed list of which services implement these storage patterns, see the **[Service & Module Inventory](docs/ServiceInventory.md)**.
