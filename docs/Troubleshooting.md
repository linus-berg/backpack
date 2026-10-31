# Troubleshooting

Backpack is a chain of services connected by RabbitMQ queues. When an artifact doesn't
show up in storage, find the step where it stopped.

## How an artifact moves through the system

Adding an artifact (GUI, `POST /api/artifact`, or a schedule):

1. **Integration.API** stores the artifact in MongoDB (collection `<processor id>`,
   marked `root: true`) and then, depending on the
   [processor definition](Configuration.md#processor-definitions):
   - `requires_approval` and the user isn't an administrator: stores it in
     `pending-approvals` instead, and stops until an administrator approves it.
   - `is_external`: stops. Nothing is processed or collected.
   - `direct_collect`: sends an `ArtifactCollectRequest` for the artifact ID (a URL)
     straight to `collector-<scheme>` (step 6).
   - otherwise: sends an `ArtifactIngestRequest` to `gateway-ingest-unprocessed`.
2. **Core.Gateway** creates an ingest context (a Redis set named by a new GUID, kept for
   two days) and sends an `ArtifactProcessRequest` to `processor-<processor id>`.
3. **The processor** fetches the metadata upstream and replies with an
   `ArtifactProcessedRequest` (every version, its files and the direct dependencies) to
   `gateway-ingest-processed`.
4. **Core.Gateway** compares the reply with MongoDB.
   - Unchanged: stops. The dependencies are **not** visited.
   - Changed: saves it, sends any `collect_requests` from the processor directly to
     collectors, sends an `ArtifactRouteRequest` to `collector-router`, and for each
     dependency not yet in this ingest context adds it (as a non-root artifact, if it's
     new) and goes back to step 2 with the same context.
5. **Collector.Router** keeps the versions that match the artifact's filter and sends one
   `ArtifactCollectRequest` per file to `collector-<URL scheme>`, e.g. `collector-https`.
6. **The collector** checks whether the object exists in the bucket. If it does, and the
   request isn't forced, it does nothing. Otherwise it downloads the file, stores it, and
   records a delta link.

**Re-track** (per artifact, per processor, or everything, and what schedules do) repeats
this from step 1 for **root** artifacts only. Since step 4 stops at unchanged artifacts, a
dependency's new versions are picked up when an artifact that depends on it changes, or
when the dependency is added as a root itself.

**Validate** re-sends the stored artifact to the router (step 5) without asking the
processor again, so missing files are collected. Validate with *force* on a
direct-collect artifact collects it again even if it exists.

## Where to look

### Status page (queues)

The Status page shows every queue with its message count and consumer count, from the
RabbitMQ management API.

- **0 consumers**: the service for that queue isn't running, or is connected to another
  RabbitMQ or vhost. Messages wait until it starts.
- **Messages piling up**: the service is running but slower than the incoming work. Each
  replica processes up to 10 messages at a time (5 for the container collectors, 1 for
  Collector.DockerArchive); add replicas.
- **A queue that should exist doesn't**: queues are created when their service first
  starts. An ingest for processor `foo` goes to `processor-foo`, so the processor
  definition's ID must match the endpoint the service registers.

### Service logs

Failures are only visible in the logs of the service that failed: the console, plus the
OpenTelemetry pipeline when `BP_OTEL_HOST` is set (Integration.API also writes
`backpack_api.log` to `BP_LOGS`). The System Events page lists what **users** did (added,
approved, deleted, re-tracked); processors and collectors don't report errors there.

To follow one artifact, search the gateway log for `INGESTED:<processor>:<id>` and
`ARTIFACT:UPDATED:<id>`, then the processor's and collectors' logs for its ID or URLs.

### Failed messages are discarded

Processors and collectors retry a message only when it fails with
`ArtifactTimeoutException`: five times immediately, then again after 5, 15 and 30
minutes. Any other exception, including `ArtifactMetadataException` (which processors
throw when an upstream registry's metadata can't be fetched or read), fails the message
at once, and processors and collectors **discard** failed messages. There is nothing to
inspect or replay; fix the cause and re-track or validate the artifact.

The gateway is different: it retries every failure five times within a second, then moves
the message to `<queue>_error` (e.g. `gateway-ingest-processed_error`), as MassTransit
does by default. The Status page hides `_error` queues, so check them in the RabbitMQ
management UI.

### MongoDB

Database `backpack`:

| Collection | Contents |
|------------|----------|
| `<processor id>`, e.g. `npm` | Artifacts: `_id`, `root`, `filter`, `config`, `versions` (each with its `files`), `dependencies` |
| `backpack-processors` | Processor definitions |
| `pending-approvals` | Artifacts waiting for approval |
| `backpack-events` | System events |
| `backpack-schedules` | Schedules |
| `backpack-api-keys`, `backpack-news` | API keys, news posts |

An artifact whose `versions` stay empty was never answered by its processor. If the
versions are there but files are missing from the bucket, the problem is in the router or
a collector.

## Common problems

| Symptom | Likely cause |
|---------|--------------|
| Added artifact never gets versions | Processor not running (0 consumers on `processor-<id>`), the processor ID doesn't match, or the processor failed (its log). |
| Versions in MongoDB, no files in the bucket | Router or collector for the file's scheme not running, a filter that matches no version, the collector can't reach the URL, or S3 credentials or `BP_S3_REGION` missing (the collector fails at startup). |
| Some files are silently missing | Collector.Http and Collector.Huggingface skip any URL that doesn't answer with a 2xx status without logging an error. Check that the URL works from the collector's network. |
| Git repository not updated | Collector.Git logs the failure and completes the message; nothing retries it until the next re-track or validate. Check its log (credentials, proxy, the 120-minute clone/fetch limit). |
| Nothing collected for an artifact with a semver filter | A `SEMVER_RANGE` filter fails on any version string that isn't semver (container tags, Git commit IDs, PEP 440 pre-releases), and then **no** file of that artifact is routed. Use a regular expression for such ecosystems. |
| Schedule never runs | Schedules use Quartz cron syntax with seconds; see [Schedules](Configuration.md#schedules). |
| New dependency versions aren't collected | Expected: see re-tracking above. Add the dependency as a root artifact, or validate it. |
| Re-track does nothing | The processor returned the same metadata, so there was nothing new. |
| Every request fails with 401 | The token's issuer doesn't match `BP_OIDC_AUTHORITY` (watch the trailing slash and the host name), or the API can't reach the authority to fetch its keys. |
| Signed in, but no admin pages | The token has no `Administrator` role in `resource_access.<BP_OIDC_AUDIENCE>.roles` or `roles`. |
| Admin API key gets 401 | Managing API keys and purging queues accept only OIDC tokens, not API keys. |
| Service exits at startup with `<NAME> is null!` | A required variable isn't set; see [Configuration](Configuration.md). |
| Status page is empty or fails | `BP_RABBIT_MQ_API` (management API) unreachable from the API, or the RabbitMQ user lacks management access. |
| Large downloads fail | Collectors buffer the whole file in the temp directory before uploading. Check its free space. |
