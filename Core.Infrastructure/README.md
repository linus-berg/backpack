# Core.Infrastructure

Implementations of the `Core.Services` interfaces, backed by MongoDB, Redis, MassTransit and the
RabbitMQ management API. Used by Core.Gateway, Integration.API and Tracker.Scheduler (and
referenced by Backpack.Tester).

## MongoDatabase (`ICoreDatabase`)

Connects with `BP_MONGO_STR` (no default) and always uses the database `backpack`. It is
registered as scoped. In Core.Gateway and Integration.API that means a missing or malformed
`BP_MONGO_STR` only shows up when the first message or request needs the database, not at
startup. Tracker.Scheduler uses the database during startup, so it fails immediately.

| Collection | Contents | `_id` |
| :--- | :--- | :--- |
| `<processor id>`, e.g. `npm` | `Artifact` documents of that processor, roots and dependencies | artifact id |
| `backpack-processors` | `Processor` documents | processor id |
| `backpack-schedules` | `Schedule` documents | schedule id (GUID string) |
| `backpack-jobs` | `Job` documents: background re-track and validation jobs. A unique partial index on `lock_key` lets only one run; finished jobs expire after 90 days. | job id (GUID string) |
| `backpack-events` | `Event` audit log; read newest first, 100 by default | GUID string |
| `pending-approvals` | `PendingArtifact` requests awaiting approval | artifact id |
| `backpack-api-keys` | `ApiKey` documents; the key is stored in plain text in `key` | GUID string |
| `backpack-news` | `NewsPost` documents; read newest first, 50 by default | GUID string |

`UpdateArtifact`, `UpdateProcessor` and `UpdateSchedule` replace the document with the same
`_id` and do not upsert, so updating a document that does not exist does nothing. The code
creates no indexes. `GetApiKey(key)` finds a key by an exact match on `key`.

## CoreCache (`ICoreCache`)

Records which artifacts have already been processed in a processing context, so the Gateway
does not process a dependency twice while resolving one tree.

| Key | Type | Members | TTL |
| :--- | :--- | :--- | :--- |
| `<context GUID>`, e.g. `3f2b6c1e-...` | Set | artifact ids | 2 days, set when the context is created |

- `InitKey(id)` creates a new GUID, adds `id` to the set and sets the 2-day expiry.
- `AddToCache(id, ctx)` adds `id` (`SADD`) and leaves the TTL as it is.
- `InCache(id, ctx)` returns `false` if the key does not exist, otherwise `SISMEMBER`.

Keys have no prefix and use Redis database 0. Only artifact ids are stored, not processor ids.

## ArtifactService (`IArtifactService`)

Sends messages through the `ISendEndpointProvider` of the calling scope. Inside a Gateway
consumer that goes through the consumer's in-memory outbox.

- `AddArtifact` returns the stored artifact if one exists (unchanged), otherwise inserts one
  with the given filter, config and `root` flag.
- `Ingest` sends `ArtifactIngestRequest` to `gateway-ingest-unprocessed`.
- `Process(artifact)` calls `InitKey(artifact.id)` and sends `ArtifactProcessRequest` with the
  new context to `processor-<artifact.processor>`. With an existing context it adds the id to
  that context.
- `Route` sends `ArtifactRouteRequest` to `collector-router`.
- `Collect` sends `ArtifactCollectRequest` to `collector-<scheme of location>`. It throws
  `InvalidCollectLocationException` for a location without a scheme.
- `Track(id, processor)` returns `false` if the artifact is not stored. Otherwise it collects
  `artifact.id` for a `direct_collect` processor and ingests the artifact for any other.
- `Track(processor)` does that for every root artifact of the processor, and `Track()` for
  every processor. Errors are logged per artifact.
- `Validate(id, processor, force)` throws `ArgumentException` if the artifact is not stored. For
  a `direct_collect` processor it collects `artifact.id` with `force`; otherwise it routes the
  stored artifact.
- `Validate(processor)` and `Validate()` validate every artifact, including non-root
  dependencies, of one or all processors. Errors are logged per artifact and per processor.

Unknown processor ids are not checked. `GetProcessor` returns `null`, and `Track` and
`Validate` then throw `NullReferenceException`.

## RabbitMqStatusService (`IStatusService`)

Calls the management API at `BP_RABBIT_MQ_API` with HTTP basic auth (`BP_RABBIT_MQ_USER`,
`BP_RABBIT_MQ_PASS`).

- `QueueStatus()` reads `GET api/queues`. It keeps queues whose names start with `collector`,
  `gateway`, `processor` or `system` (case-insensitive) and skips names that contain `error`.
  Egress is the ack rate and ingress the publish rate.
- `PurgeQueue(name)` sends `DELETE api/queues/%2F/<name>/contents` (vhost `/`). It accepts any
  queue name.

## ProcessorHost

`ProcessorHost.Create(args, registration, configure)` builds a host with Kernel logging,
telemetry, `Register(registration)` and an idle `HeartbeatWorker`. No project in the solution
calls it.

## Dependencies

The project file references `Dapper`, `Dapper.Contrib` and `Microsoft.Data.SqlClient`. None of
them are used; all storage is MongoDB and Redis.
