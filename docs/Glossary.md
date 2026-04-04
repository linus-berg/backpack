# Glossary of Terms

This glossary defines the core terminology used throughout the Backpack ecosystem to ensure consistent communication across development, operations, and architectural design.

---

### 🟢 Core Architectural Components

#### **Collector**
A protocol-specific microservice responsible for the physical retrieval of artifacts from upstream sources (e.g., HTTP, Git, OCI Registry). Collectors are "workers" that execute the actual download and persistence to S3-compatible storage. See the **[Service Inventory](ServiceInventory.md)** for a full list.

#### **Processor**
An ecosystem-specific microservice responsible for metadata extraction and dependency graph resolution. Processors understand the logic of a specific package manager (e.g., NPM, NuGet) and identify which specific files need to be collected. See the **[Service Inventory](ServiceInventory.md)** for a full list.

#### **Gateway (Core.Gateway)**
The central orchestrator and message bus of the system. It manages the ingestion state machine, routes requests between Processors and Collectors, and ensures metadata consistency.

#### **Tracker (Tracker.Scheduler)**
The scheduling engine (powered by Quartz.NET) responsible for monitoring external registries for updates and triggering periodic synchronization jobs.

#### **Integration.API**
The RESTful interface for external management, monitoring, and triggering manual ingestion requests. It is secured via OpenID Connect (OIDC).

---

### 🔵 Functional Concepts

#### **Air-Gap**
A high-security network environment that is physically and logically isolated from the public internet. Backpack’s primary goal is to provide these environments with up-to-date software mirrors.

#### **Exhaustive Collection**
The default exhaustive synchronization logic. When an artifact is requested, Backpack recursively identifies and collects **every version** and **every dependency version** until the entire dependency tree is mirrored locally.

#### **Mirroring**
The comprehensive process of creating and maintaining a synchronized, local copy of an external artifact registry or repository.

#### **Delta-Link**
The internal logic and tracking mechanism used to identify and process only the changes (deltas) that have occurred in a registry since the last successful synchronization cycle.

#### **Unified Storage**
The standardized, hierarchical S3 key structure (`s3://<bucket>/<module>/<location>`) used by all collectors to ensure consistent artifact addressing across the entire system.

---

### 🟡 Technical Terms

#### **MassTransit**
The distributed application framework used for message-based communication between Backpack microservices.

#### **Skopeo**
The underlying utility used by `Collector.Container` and `Collector.DockerArchive` for inspecting and synchronizing container images across various registry types.

#### **Git Bundle**
(Legacy Reference) A single file containing a Git repository's objects and references. While Backpack previously relied on bundles for mirroring, it now utilizes full repository synchronization for the `Collector.Git` service. The `Backpack.GitUnpack` utility remains available for legacy bundle processing.
