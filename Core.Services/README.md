# Core.Services

Interfaces for the core services. This project contains no implementations; those are in
`Core.Infrastructure`. Core.Gateway, Integration.API and Tracker.Scheduler register the
implementations and depend on these interfaces.

| Interface | Implementation (`Core.Infrastructure`) | Purpose |
| :--- | :--- | :--- |
| `IArtifactService` | `Services/ArtifactService` | Add artifacts and send ingest, process, route and collect messages; track and validate. |
| `ICoreDatabase` | `MongoDatabase` | CRUD for artifacts, processors, schedules, events, pending approvals, API keys and news posts. |
| `ICoreCache` | `CoreCache` | Redis sets that record which artifacts were already processed in a processing context. |
| `IStatusService` | `Services/RabbitMqStatusService` | Queue statistics and queue purging through the RabbitMQ management API. |

## IArtifactService

| Method | Effect |
| :--- | :--- |
| `AddArtifact(id, processor, filter, config, root)` | Returns the stored artifact if it exists (unchanged), otherwise inserts a new one. |
| `Ingest(artifact)` | Sends `ArtifactIngestRequest` to `gateway-ingest-unprocessed`. |
| `Process(artifact, existing_ctx)` | Creates a processing context (or adds to `existing_ctx`) and sends `ArtifactProcessRequest` to `processor-<processor>`. |
| `Route(artifact)` | Sends `ArtifactRouteRequest` to `collector-router`. |
| `Collect(location, processor, force)` / `Collect(request)` | Sends `ArtifactCollectRequest` to `collector-<scheme>`. |
| `Track(id, processor)`, `Track(processor)`, `Track()` | Re-ingests root artifacts, or collects them directly for `direct_collect` processors. |
| `Validate(...)` overloads | Re-routes stored artifacts, or collects them directly (optionally with `force`) for `direct_collect` processors. |

See `Core.Infrastructure/README.md` for the exact behaviour, the MongoDB collections and the
Redis keys.

The project file references `Dapper.Contrib` (including a `HintPath` into a local NuGet cache)
and `MongoDB.Bson`. No code in this project uses Dapper.
