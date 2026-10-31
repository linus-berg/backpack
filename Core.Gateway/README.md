# Core.Gateway

Worker service that coordinates artifact processing. It sends ingested artifacts to their
processor and stores the processed result in MongoDB. It then hands the files to the collectors
and walks the dependency tree, using a Redis set per processing context to avoid processing an
artifact twice. It has no HTTP endpoint.

## Queues

| Queue | Consumer | Message | Action |
| :--- | :--- | :--- | :--- |
| `gateway-ingest-unprocessed` | `IngestConsumer` | `ArtifactIngestRequest` | Start a new processing context. |
| `gateway-ingest-processed` | `ProcessedConsumer` | `ArtifactProcessedRequest` | Handle a processor's reply. |
| `gateway-ingest-processed-raw` | `ProcessedRawConsumer` | `ArtifactProcessedRequest` | Same as above, for plain JSON messages without a MassTransit envelope. |
| `system-event` | `SystemEventConsumer` | `SystemEventMessage` | Insert an `Event` into `backpack-events`. |

Outgoing messages:

| Message | Queue |
| :--- | :--- |
| `ArtifactProcessRequest` | `processor-<artifact.processor>` |
| `ArtifactCollectRequest` | `collector-<scheme of location>` |
| `ArtifactRouteRequest` | `collector-router` |

## Flow

1. **Ingest.** `IngestConsumer` calls `ArtifactService.Process`. A new context GUID is created
   in Redis: a set containing the artifact id, expiring after 2 days. An
   `ArtifactProcessRequest { ctx, artifact }` goes to `processor-<processor>`.
2. **Processor reply.** The processor answers with `ArtifactProcessedRequest { context,
   artifact, collect_requests }`, which is handled by `GatewayProcessingService.ProcessArtifact`.
3. **Change check.** The stored artifact is loaded from the collection named after the
   processor. The reply counts as changed if nothing is stored, or if any of these differ: `id`,
   `processor`, `filter`, `root`, `config`, the version keys, each version's `status` and
   files (name, `uri`, `folder`), or the set of dependency ids. `filter_type`, `status` and
   dependency `processor` and `config` are not compared.
4. **Changed.** The stored document is replaced with the artifact from the reply. Every
   entry in `collect_requests` is sent to `collector-<scheme>`. An `ArtifactRouteRequest` with
   the artifact is sent to `collector-router`, which sends collect requests for the files of
   versions that match the filter.
5. **Unchanged.** Processing stops. Nothing is collected and dependencies are not visited.
6. **Dependencies** (changed only). For each dependency whose id is not in the context set,
   `AddArtifact(id, processor, "", config, root: false)` returns the stored artifact or
   inserts a new one. The id is added to the context set and an `ArtifactProcessRequest`
   with the same context is sent, which continues at step 2.

### Raw endpoint

`gateway-ingest-processed-raw` uses MassTransit's raw JSON deserializer, so services that do
not use MassTransit can reply with a plain JSON body. The body is an `ArtifactProcessedRequest`,
not a bare `Artifact`:

```json
{
  "context": "<ctx GUID from the ArtifactProcessRequest>",
  "artifact": { "id": "my-package", "processor": "pypi", "root": true, "versions": {}, "dependencies": [] },
  "collect_requests": []
}
```

## Endpoint settings and failures

All four endpoints (`Definitions/BaseGatewayDefinition.cs`) allow 10 concurrent messages, retry
any exception after 100, 200, 500, 800 and 1000 ms, and use an in-memory outbox. Messages the
consumer sends are only delivered after it completes. This is not the module policy from
`Core.Kernel`: faults are not discarded, so after the last retry MassTransit moves the message
to `<queue>_error` (e.g. `gateway-ingest-processed_error`). The Status page of the API hides
queues whose name contains `error`.

The MongoDB update and the Redis writes are not part of the outbox. Suppose an attempt fails
after the artifact was saved, for example on an `InvalidCollectLocationException` from an
entry in `collect_requests`. The retry then finds the artifact unchanged and stops at step 5.
Collect requests, the route request and the dependency messages from the failed attempt are
never sent. Validate (see Integration.API) re-routes the stored artifact. It does not resend
`collect_requests` or visit dependencies.

## Storage

- MongoDB database `backpack`: artifact collections named after the processor, and
  `backpack-events`.
- Redis: one set per context, key = context GUID, members = artifact ids, TTL 2 days. See
  `Core.Infrastructure/README.md`.

## Configuration

| Variable | Default | Effect |
| :--- | :--- | :--- |
| `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` | `localhost`, `guest`, `guest` | RabbitMQ, vhost `/`. |
| `BP_REDIS_HOST`, `BP_REDIS_USER`, `BP_REDIS_PASS` | `localhost`, `default`, `myverylongpassword` | Redis. Connected during startup; startup fails if Redis is unreachable. |
| `BP_MONGO_STR` | none | MongoDB. Read when the first message is consumed, not at startup. |
| `BP_OTEL_HOST` | `http://localhost:4318` | OTLP gRPC export of logs, traces and metrics; `""` disables it. |

`dotnet run --project Core.Gateway` uses `Properties/launchSettings.json` (RabbitMQ and Redis on
localhost, `mongodb://root:example@localhost:27017`). The image is built from the repository
root with `docker build -f Core.Gateway/Dockerfile .`.

## Gotchas

- The artifact in the processor's reply replaces the stored document completely. A processor
  must return `root`, `filter`, `filter_type` and `config` as it received them; otherwise,
  for example, a root artifact becomes a non-root one.
- If no document is stored (e.g. the artifact was deleted while it was being processed), the
  update does not insert one. Collection and dependency processing still run.
- Dependencies are only visited below artifacts that changed. Tracking only re-ingests root
  artifacts. A dependency is therefore only re-processed when the artifact that depends on it
  changes too.
- The context set is keyed by artifact id only. Two dependencies with the same id but
  different processors in one tree are processed only once.
- `AddToCache` does not set a TTL. If a context outlives its 2-day expiry, the next dependency
  recreates the key without an expiry.
- `Worker` is an idle loop; all work happens in the consumers.
