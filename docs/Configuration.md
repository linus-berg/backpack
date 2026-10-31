# Configuration Reference

Backpack services are configured with environment variables. Shared settings and their
defaults live in `Core.Kernel/Configuration.cs` and `Core.Kernel/CoreVariables.cs`; a few
module-specific variables are read directly by the module that needs them.

A variable without a default is **required**: the service throws
`NoNullAllowedException: <NAME> is null!` the first time it reads it, usually at startup.
Setting a variable to an empty string counts as set.

Processors (the ecosystems Backpack knows about) are not configured with environment
variables but stored in MongoDB; see [Processor definitions](#processor-definitions).

## Shared by every service

| Variable            | Default                 | Notes |
|---------------------|-------------------------|-------|
| `BP_RABBIT_MQ_HOST` | `localhost`             | RabbitMQ host (AMQP, vhost `/`). |
| `BP_RABBIT_MQ_USER` | `guest`                 | Also used for the management API (see the API below). |
| `BP_RABBIT_MQ_PASS` | `guest`                 | |
| `BP_OTEL_HOST`      | `http://localhost:4318` | OTLP endpoint for traces, metrics and logs, sent over **gRPC**. Set it to an empty string to turn telemetry off. |

`BP_OTEL_HOST` always has a value unless you clear it, so every service tries to export
telemetry by default. The default points at port 4318, which is normally the OTLP/HTTP
port; an OpenTelemetry Collector receives gRPC on 4317, so set e.g.
`http://otel-collector:4317`.

What is exported: traces and metrics from every service except Processor.Pypi, which
doesn't set up telemetry. Logs only from Core.Gateway, Collector.Git and processors
generated from the template, which send them to OTLP and the console. Every other service logs to the console only, and
Integration.API additionally to a file.

## Integration.API, Core.Gateway and Tracker.Scheduler

These three use MongoDB and Redis.

| Variable         | Default              | Notes |
|------------------|----------------------|-------|
| `BP_MONGO_STR`   | —                    | **Required.** MongoDB connection string, e.g. `mongodb://user:pass@mongo:27017`. The database is always named `backpack`. |
| `BP_REDIS_HOST`  | `localhost`          | `host` or `host:port`. |
| `BP_REDIS_USER`  | `default`            | |
| `BP_REDIS_PASS`  | `myverylongpassword` | The defaults match the development Compose stack. Set both for any other Redis. |

All three connect to Redis when they start and fail to start if it is unreachable.

## Integration.API

| Variable            | Default                                | Notes |
|---------------------|----------------------------------------|-------|
| `BP_OIDC_AUTHORITY` | `http://localhost:8090/realms/backpack` | OIDC issuer. The API downloads its metadata and validates token issuers against it (with or without a trailing slash). The GUI gets this value from `GET /api/auth/oidc` and sends the browser to it, so it must be the URL **users** reach, and the API must be able to reach it too. |
| `BP_OIDC_AUDIENCE`  | `backpack`                             | OIDC client ID. The GUI signs in with it, and roles are read from the token's `resource_access.<audience>.roles` claim. The `aud` claim is **not** validated. |
| `BP_RABBIT_MQ_API`  | `http://localhost:15672`               | RabbitMQ management API, used for the Status page and queue purging. Uses `BP_RABBIT_MQ_USER`/`BP_RABBIT_MQ_PASS`. |
| `BP_LOGS`           | `logs/`                                | Directory for the API's own log file, `backpack_api.log`. No other service writes log files. |

The API listens on the ASP.NET Core default: port **8080** in the published container
image. Set `ASPNETCORE_HTTP_PORTS` (or `ASPNETCORE_URLS`) to change it. The GUI expects the
API at `<protocol>//<GUI hostname>:8004/api`, so publish or route the API on port 8004 of
the same hostname as the GUI. See `Integration.API/README.md` for endpoints, roles and API
keys.

## Collectors that write to S3

Collector.Http, Collector.Huggingface, Collector.Git, Collector.Wget and
Collector.DockerArchive store artifacts in S3-compatible storage.

| Variable           | Default | Notes |
|--------------------|---------|-------|
| `BP_S3_ENDPOINT`   | —       | **Required.** `host:port`. Prefix with `https://` for TLS; a bare `host:port` or `http://` uses plain HTTP. |
| `BP_S3_ACCESS_KEY` | —       | **Required.** |
| `BP_S3_SECRET_KEY` | —       | **Required.** |
| `BP_S3_BUCKET`     | —       | **Required.** Created on first use if it doesn't exist and the key is allowed to create it. |
| `BP_S3_REGION`     | —       | **Required**, even if your storage ignores regions. An empty value is accepted. |
| `BP_S3_TRACING`    | `false` | `true` logs every S3 request and response (very verbose). |

Uploads whose source can't be rewound (HTTP response bodies, for example) are first
buffered in full in the system temp directory (`TMPDIR`, usually `/tmp`), so collectors
need temporary space for the largest file they download.

How objects are named is described in [Storage Architecture](../STORAGE.md).

## Module-specific variables

| Variable                          | Read by | Default | Notes |
|-----------------------------------|---------|---------|-------|
| `BP_COLLECTOR_DIRECTORY`          | Collector.Git, Collector.DockerArchive | `/data/` | Local working directory; each collector uses a subdirectory (`git/`, `docker-archive/`). Use a persistent volume for Git, which keeps its clones there between runs. |
| `BP_COLLECTOR_HTTP_DELTA`         | Collector.Http, Collector.Huggingface | `true` | `true` records a delta link for every newly collected file; see [Delta links](../STORAGE.md#delta-links). |
| `BP_COLLECTOR_HTTP_MODE`          | Collector.Http | `lake` | Read but currently has no effect. |
| `BP_COLLECTOR_CONTAINER_REGISTRY` | Collector.Container (via Library.Skopeo) | — | **Required**; read for every request, which fails if it's unset. The registry images are copied to, e.g. `registry.internal:5000`. |
| `BP_HF_TOKEN`                     | Collector.Huggingface | — | Optional Hugging Face token, needed for gated and private models. |
| `BP_CREDENTIALS_FILE`             | Collector.Git | — | JSON file with per-host credentials for private repositories. See `Collector.Git/README.md`. |
| `HTTPS_PROXY`                     | Collector.Git | — | Passed to `git` as `http.proxy`. |
| `GITHUB_API_KEY`                  | Library.Github (Processor.Github.Releases) | — | Optional GitHub token. Unauthenticated requests are heavily rate limited. |
| `ARTIFACTHUB_API_KEY_ID`, `ARTIFACTHUB_API_KEY_SECRET` | Processor.Helm | — | Optional Artifact Hub API key. |
| `SCHEDULE_FILE`                   | Tracker.Scheduler | — | Optional schedule file; see `Tracker.Scheduler/README.md`. |
| `GIT_BUNDLE_INPUT`, `GIT_BUNDLE_ARCHIVE`, `GIT_BUNDLE_REPOS` | Backpack.GitUnpack | — | Required by that tool only; see `Backpack.GitUnpack/README.md`. |

`BP_API_HOST` is declared in `CoreVariables` but no service reads it.

Each module's README lists anything else specific to it.

## Processor definitions

Every ecosystem is a document in the MongoDB collection `backpack-processors` (database
`backpack`). Administrators manage them in the GUI (Processors page) or through
`/api/processor`. A processor that isn't defined there doesn't appear in the GUI, even if
its service is running.

| Field               | Default | Meaning |
|---------------------|---------|---------|
| `_id`               | —       | Processor ID, e.g. `npm`. Artifacts are sent to the queue `processor-<id>` and stored in a MongoDB collection named `<id>`. It must match the endpoint the processor service registers. |
| `description`       | `""`    | Shown in the GUI (Markdown; legacy HTML is still rendered). |
| `direct_collect`    | `false` | The artifact ID is a URL that is sent straight to a collector (`collector-<scheme>`), with no processor involved, e.g. `git://github.com/owner/repo`. |
| `requires_approval` | `false` | Artifacts added by non-administrators wait in `pending-approvals` until an administrator approves them. |
| `multi_add`         | `false` | The GUI offers bulk add (one artifact per line). |
| `is_external`       | `false` | Artifacts are only recorded; nothing is processed or collected. For ecosystems that are synchronised by something outside Backpack. |
| `preview_enabled`   | `true`  | The GUI's add form can ask the processor for a preview (queue `processor-<id>-preview`). Processors created through the API start with `false`. |
| `config`            | `{}`    | Extra per-artifact fields, keyed by field key: `{ "key", "name", "type", "placeholder", "default" }`. Their values end up in `artifact.config` for the processor to read; each processor README lists the keys it understands. |

Creating a processor through the API (`POST /api/processor`) sets only the ID and flags;
`direct_collect`, the description and `config` are set with `POST /api/processor/update`,
which is what the GUI's processor editor does. The development stack seeds a few
definitions from `Compose/mongo-init.js`.

## Schedules

Tracker.Scheduler re-tracks every root artifact of a processor on a schedule. Schedules
are stored in MongoDB (`backpack-schedules`) and edited on the GUI's Scheduler page; the
scheduler reloads them when they change. `SCHEDULE_FILE` only seeds an empty collection.

- Expressions are **Quartz** cron expressions: six or seven fields, **seconds first**, and
  one of day-of-month and day-of-week must be `?`. Daily at 03:00 is `0 0 3 * * ?`; a
  five-field Unix expression such as `0 3 * * *` is not valid.
- They run in the scheduler's local time zone (`TZ`).
- Only one schedule per processor takes effect.
- The API checks expressions with a different library (Cronos), so it accepts some that
  Quartz rejects, and the *next run* it shows is in UTC. If a schedule never runs, check
  the scheduler's log for a parse error.

## Ports

| Service                         | Port |
|---------------------------------|------|
| GUI                             | 3000 in development (`yarn start`), 80 in its container |
| Integration.API                 | 8080 in its container (8004 expected by the GUI; 8004 in the local launch profile) |
| RabbitMQ AMQP / management      | 5672 / 15672 |
| MongoDB                         | 27017 |
| Redis                           | 6379 |
| Keycloak (development Compose)  | 8090 |
| S3 (development Compose, SeaweedFS) | 9000 (S3 API), 9001 (admin UI) |
| Container registry (development Compose) | 6000 |
