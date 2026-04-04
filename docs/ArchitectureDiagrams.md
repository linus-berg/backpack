# Architecture Diagrams

## Advanced Architectural View
This diagram illustrates the high-level orchestration, state management, and storage flows within the Backpack ecosystem.

```mermaid
graph TB
    subgraph "External Realms"
        REG[Public Registries: NPM, OCI, PyPi, etc.]
        OIDC[OIDC Provider: Keycloak/Auth0]
    end

    subgraph "Entry Points"
        GUI[Backpack Web GUI]
        API[Integration.API]
    end

    subgraph "Orchestration Layer"
        GW(Core.Gateway)
        SCHED[Tracker.Scheduler]
        BUS((RabbitMQ / MassTransit))
    end

    subgraph "Logic & Retrieval"
        PROC{Processors}
        ROUTER[Collector.Router]
        COLL{Collectors}
    end

    subgraph "Persistence & State"
        MONGO[(MongoDB: Metadata)]
        REDIS[(Redis: Cache/Rate-Limit)]
        S3[(S3/MinIO: Persistent Storage)]
        DISK[(Local Disk: Transient Storage)]
    end

    %% Interaction Flow
    GUI -- "Auth" --> OIDC
    API -- "Validate" --> OIDC
    GUI -- "Trigger Ingest" --> API
    SCHED -- "Scheduled Ingest" --> BUS
    API -- "Ingest Request" --> BUS
    BUS --> GW
    
    GW -- "State & History" --> MONGO
    GW -- "Route Process" --> BUS
    BUS --> PROC
    
    PROC -- "Fetch Metadata" --> REG
    PROC -- "Resolve Deps" --> BUS
    BUS -- "Processed Response" --> GW
    
    GW -- "Recursive Discovery" --> BUS
    
    GW -- "Route Collect" --> BUS
    BUS --> ROUTER
    ROUTER --> COLL
    
    COLL -- "Existence Check" --> S3
    COLL -- "Download" --> REG
    COLL -- "Atomic Write" --> DISK
    DISK -- "Finalize" --> S3
    
    COLL -- "Locking" --> REDIS
```

## Message Propagation Diagram
The following diagram illustrates the simplified flow of messages and artifact ingestion requests.

```mermaid
graph TD
    API[Integration.API] -- ArtifactIngestRequest --> GW(Core.Gateway)
    SCHED[Tracker.Scheduler] -- ArtifactIngestRequest --> GW
    GW -- ArtifactProcessRequest --> PROC{Processors}
    PROC -- ArtifactProcessedRequest --> GW
    GW -- ArtifactRouteRequest --> ROUTER[Collector.Router]
    ROUTER -- ArtifactCollectRequest --> COLL{Collectors}
    COLL -- Download --> STORAGE[(S3 Storage)]

    subgraph Processors
        PROC_NPM[Processor.Npm]
        PROC_PYPI[Processor.Pypi]
        PROC_...[...]
    end

    subgraph Collectors
        COLL_HTTP[Collector.Http]
        COLL_GIT[Collector.Git]
        COLL_...[...]
    end
```
