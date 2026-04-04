# Backpack Storage Architecture

This document describes how artifacts are stored in S3 (or S3-compatible storage like MinIO) and the operational logic of the various collectors.

## S3 Directory Structure

All collectors utilize a unified storage structure managed by the `Collector.Kernel.FileSystem` abstraction. The root of the bucket follows a hierarchical pattern:

`s3://<bucket-name>/<module>/<location>`

- **`<module>`**: Typically refers to the ecosystem or processor type (e.g., `npm`, `nuget`, `git`, `docker`).
- **`<location>`**: The unique path or identifier for the artifact within that ecosystem.

### Examples:
- **NPM**: `s3://backpack/npm/lodash/-/lodash-4.17.21.tgz`
- **NuGet**: `s3://backpack/nuget/Newtonsoft.Json/13.0.1/newtonsoft.json.13.0.1.nupkg`
- **Git**: `s3://backpack/git/github.com/dotnet/runtime.git/runtime@...bundle`

---

## Collectors

Collectors are responsible for the physical retrieval of artifacts from upstream sources and persisting them to the storage layer.

### 1. Collector.Http
The primary collector for generic web resources.
- **Logic**: Performs a standard HTTP GET request to the upstream URI.
- **Delta Support**: Implements existence checks to avoid redundant downloads.
- **Storage**: Maps the URI path directly to the S3 `<location>`.

### 2. Collector.Git
Produces and manages full Git repository mirrors.
- **Logic**: Performs a full repository synchronization using standard Git protocols.
- **Storage**: Stores the repository in its native Git structure, allowing for full integrity verification and downstream mirroring.

### 3. Collector.Container (Skopeo)
Handles container image mirroring between registries.
- **Logic**: Wraps the `skopeo` library to interact with remote Docker registries.
- **Storage**: Stores images as OCI-compliant artifacts or exploded layers depending on configuration.
- **Key Feature**: Can copy images between different registry types while maintaining layer integrity.

### 4. Collector.DockerArchive
Fetches container images from remote registries and saves them as Docker archives (`.tar`).
- **Logic**: Uses Skopeo to pull remote images and encapsulate them into a single TAR archive.
- **Storage**: Persists the resulting `.tar` file to the unified S3 structure.
- **Use Case**: Preferred for environments that require full Docker image archives rather than exploded OCI layers.

### 5. Collector.Wget
A specialized collector for recursive website mirroring.
- **Logic**: Uses `wget` mirrors to download entire directory structures or documentation sites.
- **Storage**: Preserves the directory hierarchy of the mirrored site under the specified `<module>` prefix.

### 6. Collector.Rsync
Used for high-speed file synchronization.
- **Logic**: Interfaces with the `rsync` protocol.
- **Value**: Extremely efficient for large repositories that provide rsync access (e.g., Linux distribution mirrors).

---

## The Storage Kernel (`Collector.Kernel`)

The `FileSystem.cs` in `Collector.Kernel` provides the unified interface used by all background services. It handles:
- **Abstractions**: Transparently switches between local disk (for temporary processing) and S3 (for permanent storage).
- **Atomic Writes**: Ensures that artifacts are fully downloaded and verified before being marked as complete in the metadata.
- **Path Resolution**: Standardizes how ecosystem-specific identifiers are translated into valid S3 keys.
