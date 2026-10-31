# Architecture Diagrams

## Components

Solid arrows are RabbitMQ messages (labelled with the queue), dashed arrows are other
connections.

```mermaid
flowchart LR
    subgraph Users
        GUI[Backpack GUI]
        CLI["Scripts / CI<br/>(API key)"]
    end
    OIDC[OIDC provider]

    subgraph Backpack
        API[Integration.API]
        SCHED[Tracker.Scheduler]
        GW[Core.Gateway]
        PROC["Processors<br/>processor-{id}"]
        ROUTER[Collector.Router]
        COLL["Collectors<br/>collector-{scheme}"]
    end

    subgraph State
        MONGO[("MongoDB<br/>artifacts, processors,<br/>schedules, events")]
        REDIS[("Redis<br/>ingest contexts")]
        S3[(S3 bucket)]
        REG[(Container registry)]
    end

    UP["Upstream registries<br/>npm, PyPI, NuGet, OCI, Git, ..."]

    GUI -.->|sign-in| OIDC
    GUI -.->|"REST + token"| API
    CLI -.->|"REST + X-API-KEY"| API
    API -.->|validate tokens| OIDC

    API -->|gateway-ingest-unprocessed| GW
    SCHED -->|gateway-ingest-unprocessed| GW
    GW -->|"processor-{id}"| PROC
    PROC -->|gateway-ingest-processed| GW
    GW -->|collector-router| ROUTER
    ROUTER -->|"collector-{scheme}"| COLL
    API -->|"collector-{scheme}<br/>(direct collect)"| COLL
    API -->|"processor-{id}-preview"| PROC
    API -->|"collector-router<br/>(validate)"| ROUTER
    API -->|system-event| GW

    API -.-> MONGO
    GW -.-> MONGO
    SCHED -.-> MONGO
    API -.-> REDIS
    GW -.-> REDIS
    SCHED -.-> REDIS

    PROC -.->|metadata| UP
    COLL -.->|download| UP
    COLL -.->|store| S3
    COLL -.->|container images| REG
```

- The **API** is the only entry point for users. It writes artifacts and settings to
  MongoDB and starts work by sending messages. Only previews wait for an answer
  (request/response).
- **Tracker.Scheduler** runs the re-track schedules stored in MongoDB.
- **Core.Gateway** is the only service that interprets processor results: it detects
  changes, stores them, and fans out to the router and to dependencies.
- **Processors** only read upstream metadata. **Collectors** only download and store.
  Neither uses MongoDB or Redis.
- `direct_collect` processors skip the gateway, processors and router: the artifact ID is
  the URL handed to a collector.

## Processing an artifact

```mermaid
sequenceDiagram
    participant API as Integration.API
    participant GW as Core.Gateway
    participant R as Redis
    participant DB as MongoDB
    participant P as processor-npm
    participant RT as collector-router
    participant C as collector-https
    participant S3 as S3 bucket

    API->>DB: store artifact (root)
    API->>GW: ArtifactIngestRequest
    GW->>R: new ingest context (GUID, 2 days)
    GW->>P: ArtifactProcessRequest(ctx, artifact)
    P->>P: fetch metadata upstream
    P->>GW: ArtifactProcessedRequest(context, artifact)
    GW->>DB: compare with stored artifact
    alt unchanged
        GW-->>GW: stop
    else changed
        GW->>DB: save artifact
        GW->>RT: ArtifactRouteRequest(artifact)
        loop each version matching the filter, each file
            RT->>C: ArtifactCollectRequest(url, module)
            C->>S3: exists?
            opt missing (or force)
                C->>C: download
                C->>S3: put object, delta link
            end
        end
        loop each dependency not yet in the context
            GW->>R: add to context
            GW->>DB: add artifact (non-root) if new
            GW->>P: ArtifactProcessRequest(ctx, dependency)
        end
    end
```

See [Troubleshooting](Troubleshooting.md) for each step in detail and
[Storage Architecture](../STORAGE.md) for the object layout.
