# Integration.API

ASP.NET Core 8 REST API used by the Backpack GUI and by scripts. It reads and writes MongoDB
directly and sends work to the Gateway and the collectors over RabbitMQ. It asks processors
for previews (request/response) and reads queue statistics from the RabbitMQ management API.
All routes are under `/api` and are case-insensitive.

## Running and ports

`dotnet run --project Integration.API` uses `Properties/launchSettings.json`, which listens on
`https://localhost:4000`, `http://localhost:4001` and `http://localhost:8004`. The profile also
sets `ASPNETCORE_ENVIRONMENT=Development` and points at RabbitMQ, Redis and MongoDB on localhost.

The GUI calls the API at `<protocol>//<GUI hostname>:8004/api`
(`backpack-frontend/src/api/config.ts`), so the API must be reachable on port 8004 of the
host that serves the GUI.

The image (`docker build -f Integration.API/Dockerfile .` from the repository root) is based on
`mcr.microsoft.com/dotnet/aspnet:8.0`, which listens on port 8080 (`ASPNETCORE_HTTP_PORTS`).
The Dockerfile's `EXPOSE 80` and `EXPOSE 443` do not change that. Publish 8004 to container
port 8080.

## Configuration

| Variable | Default | Effect |
| :--- | :--- | :--- |
| `BP_MONGO_STR` | none | MongoDB. Read on the first request that needs the database. |
| `BP_REDIS_HOST`, `BP_REDIS_USER`, `BP_REDIS_PASS` | `localhost`, `default`, `myverylongpassword` | Connected at startup; startup fails if Redis is unreachable. The API itself writes no Redis keys. |
| `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` | `localhost`, `guest`, `guest` | RabbitMQ, vhost `/`. User and password are also used for the management API. |
| `BP_RABBIT_MQ_API` | `http://localhost:15672` | Management API for the queue endpoints. |
| `BP_OIDC_AUTHORITY` | `http://localhost:8090/realms/backpack` | Token issuer and metadata source. |
| `BP_OIDC_AUDIENCE` | `backpack` | OIDC client ID; selects the client roles in the token. |
| `BP_LOGS` | `logs/` | Directory of the Serilog file `backpack_api.log`. |
| `BP_OTEL_HOST` | `http://localhost:4318` | OTLP gRPC export of traces and metrics (not logs); `""` disables it. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables Swagger. |

If `BP_OIDC_AUTHORITY` or `BP_OIDC_AUDIENCE` is set to an empty string, startup fails with
"OIDC is not configured."

## Authentication

Two schemes are registered.

**Bearer token** (`Authorization: Bearer <jwt>`) is the default scheme.
- The issuer must equal `BP_OIDC_AUTHORITY`, with or without a trailing slash. The token
  lifetime is validated. Metadata is fetched from the authority and HTTPS is not required.
- The audience is **not** validated (`ValidateAudience = false`). Any unexpired token from
  that issuer is accepted.
- The user name is the `preferred_username` claim.
- When a token is validated, every entry of `resource_access.<BP_OIDC_AUDIENCE>.roles` and
  every `roles` claim becomes a role. Realm roles (`realm_access.roles`) are not read. In
  Keycloak, give administrators the client role `Administrator` on the client whose ID is
  `BP_OIDC_AUDIENCE`.

**API key** (`X-API-KEY: <key>`, scheme name `ApiKey`, `ApiKeyAuthenticationHandler.cs`).
- The key is looked up by exact match in the MongoDB collection `backpack-api-keys`, where it
  is stored in plain text.
- If the header is missing or empty, the scheme does nothing. An unknown key fails
  authentication, which gives 401 unless a valid bearer token is also sent.
- The caller's name is the key's `name`. Its role is `Administrator` if `is_admin` is set,
  otherwise `User`.
- `POST /api/apikey` creates keys (32 hex characters). The full key is only returned in that
  response.

The default policy requires an authenticated user from either scheme. It applies wherever a
controller has a plain `[Authorize]`, including actions that add
`[Authorize(Roles = "Administrator")]`. `ApiKeyController` and
`DELETE /api/status/queue/{queue_name}` have only the role attribute, so the default policy is
not combined in. Only the bearer token is evaluated there, and an API key gets 401.

`GET /api/auth/oidc` returns `{ "authority": BP_OIDC_AUTHORITY, "client_id": BP_OIDC_AUDIENCE }`
without authentication. The GUI uses it to start the OIDC login.

## Endpoints

Auth: **anon** = no authentication; **user** = any authenticated caller (bearer or API key);
**admin** = role `Administrator` (bearer or admin API key); **admin, bearer** = role
`Administrator`, bearer token only.

| Method | Route | Auth | Description |
| :--- | :--- | :--- | :--- |
| GET | `/api/auth/oidc` | anon | OIDC authority and client ID for the GUI. |
| GET | `/api/auth/me` | user | Caller name and roles. |
| GET | `/api/apikey` | admin, bearer | List keys: `id`, `name`, `key_preview` (first and last 4 characters), `is_admin`, `created_at`, `created_by`. |
| POST | `/api/apikey` | admin, bearer | Body `{name, is_admin}`. Create a key and return it in full. |
| DELETE | `/api/apikey/{id}` | admin, bearer | Delete a key; 400 if not found. |
| GET | `/api/artifact/all?processor=&only_roots=` | user | Summaries of one processor's artifacts (version and dependency counts). |
| GET | `/api/artifact?processor=&id=` | user | Full artifact document; 404 if not found. |
| POST | `/api/artifact/preview` | user | Body `{id, processor, filter, config}`. Request to `processor-<processor lower-cased>-preview`; returns the artifact or 400 with the processor's error. Nothing is stored. |
| POST | `/api/artifact` | user | Body `{id, processor, filter, config}`. Add a root artifact (see below). |
| GET | `/api/artifact/pending` | admin | Requests waiting for approval. |
| POST | `/api/artifact/approve` | admin | Body `{id, processor}`. Create the artifact from the request, delete the request, start ingest or collect. |
| POST | `/api/artifact/reject` | admin | Body `{id, processor, reason?}`. Delete the request; the reason goes into the event log. |
| POST | `/api/artifact/track` | user | Body `{id, processor}`. Re-ingest the stored artifact (collect it for `direct_collect`); 400 if not stored. |
| POST | `/api/artifact/track/all` | admin | Track the root artifacts of every processor. |
| POST | `/api/artifact/validate` | user | Body `{id, processor, force}`. Route the stored artifact to `collector-router` (collect with `force` for `direct_collect`). |
| POST | `/api/artifact/validate/all` | admin | Validate every artifact, including dependencies, of every processor. |
| DELETE | `/api/artifact` | admin | Body `{id, processor}`. Delete the metadata document and return it; stored files are not touched. |
| POST | `/api/artifact/collect` | user | Body `{location, module}`. Send `ArtifactCollectRequest` to `collector-<scheme>`; `force` is ignored. |
| GET | `/api/container/container` | anon | Sends a hard-coded collect of `docker-archive://docker.io/nginx:latest`. |
| GET | `/api/event?limit=100` | user | Latest events from `backpack-events`, newest first. |
| GET | `/api/news?limit=50` | user | Latest news posts. |
| POST | `/api/news` | admin | Body `{title, content}`; the author is the caller. |
| DELETE | `/api/news/{id}` | admin | Delete a post; 404 if not found. |
| GET | `/api/processor/processors` | user | All processors; `config` is returned as a JSON string. |
| POST | `/api/processor` | admin | Body `{processor_id, requires_approval, multi_add, is_external, preview_enabled}`. `direct_collect` cannot be set here. |
| POST | `/api/processor/update` | admin | Body `{processor_id, description, config, direct_collect, requires_approval, multi_add, is_external, preview_enabled}`; `config` is a JSON string of auxiliary fields. |
| DELETE | `/api/processor/{id}` | admin | Delete the processor document (its artifact collection stays); 400 if not found. |
| GET | `/api/scheduler` | user | Schedules, with `next_run` (UTC) computed by Cronos. |
| POST | `/api/scheduler` | admin | Body `{processor, cron, id?}`. Add a schedule (id generated if missing) and publish `ReloadSchedulesRequest`. |
| PUT | `/api/scheduler` | admin | Body `{id, processor, cron}`. Replace the schedule with that id and publish `ReloadSchedulesRequest`. |
| DELETE | `/api/scheduler/{id}` | admin | Delete a schedule (204, or 404) and publish `ReloadSchedulesRequest`. |
| POST | `/api/scheduler/validate` | admin | Body `{processor, cron}`. Returns `{Valid, NextOccurrences}` (next 5, UTC) or `{Valid: false, Error}`. |
| POST | `/api/scheduler/trigger/{processor}` | admin | Track one processor now. `last_run` is not updated. |
| GET | `/api/status/status` | anon | Always returns `Backpack is OK.`; nothing is checked. |
| GET | `/api/status/queue` | anon | Queue name, messages, consumers and rates from the management API. |
| DELETE | `/api/status/queue/{queue_name}` | admin, bearer | Purge a queue. Any queue name is accepted. |

Cron expressions are checked with Cronos in six-field, seconds-first format. Tracker.Scheduler
runs them with Quartz, which is stricter; see `Tracker.Scheduler/README.md`.

### Adding an artifact (`POST /api/artifact`)

1. The processor is loaded from `backpack-processors`. For a `direct_collect` processor the
   `id` must be a URL with a scheme (`git://host/owner/repo`), otherwise the response is 400.
2. If the artifact is not stored yet and the processor has `requires_approval` and the caller
   is not `Administrator`, a `PendingArtifact` is added to `pending-approvals` and the request
   ends.
3. Otherwise a new artifact is stored with `root = true`. An existing non-root artifact is
   switched to `root = true`. An existing root artifact returns 200 with
   `"<processor>/<id> already Exists!"` and nothing is sent.
4. For an `is_external` processor nothing more happens. A `direct_collect` processor gets an
   `ArtifactCollectRequest` for the `id`. Any other processor gets an `ArtifactIngestRequest`
   to `gateway-ingest-unprocessed`.

## Messages sent

| Trigger | Message | Destination |
| :--- | :--- | :--- |
| Add, approve or track (non-direct) | `ArtifactIngestRequest` | `gateway-ingest-unprocessed` |
| Add, approve, track or validate (direct), `/collect` | `ArtifactCollectRequest` | `collector-<scheme>` |
| Validate (non-direct) | `ArtifactRouteRequest` | `collector-router` |
| Preview | `ArtifactPreviewRequest`, MassTransit request with the default 30 s timeout | `processor-<id>-preview` |
| Schedule add, update or delete | `ReloadSchedulesRequest` (published) | Tracker.Scheduler |
| Most write actions | `SystemEventMessage` | `system-event`; the Gateway stores it in `backpack-events` |

## Errors

- Unhandled exceptions become a 500 `application/problem+json` response (status, title,
  traceId) in every environment. No stack trace is returned.
- `InvalidCollectLocationExceptionHandler` turns `InvalidCollectLocationException` into a 400
  ProblemDetails with the title `Invalid artifact location`. `ArtifactController` returns the
  same 400 itself when a location has no scheme.
- Bodies that fail model binding (e.g. a missing `required` property) return a 400
  ValidationProblemDetails.
- Many actions return a plain string for 400 and 404, e.g. `Artifact not found`.
- A processor id that is not in `backpack-processors` makes `POST /api/artifact`, `/approve`,
  `/track`, `/validate`, `/api/processor/update` and `/api/scheduler/trigger/{processor}`
  throw, which returns 500. Validating an artifact that is not stored also returns 500.
- A preview whose processor does not answer within 30 s returns 500.

## Other behaviour

- **CORS:** any origin, any header and any method. The middleware runs before the exception
  handler, so error responses carry CORS headers too.
- **Swagger:** only with `ASPNETCORE_ENVIRONMENT=Development`. The UI is at `/swagger` and the
  document at `/swagger/v1/swagger.json`.
- **Logging:** Serilog writes to the console and to `<BP_LOGS>/backpack_api.log` (one file, no
  rolling) and logs every request. Logs are not sent over OTLP.
- **Pending requests:** a `PendingArtifact` uses the artifact id as its MongoDB `_id`. A second
  pending request for the same id fails with a duplicate-key error (500), even for a different
  processor.
