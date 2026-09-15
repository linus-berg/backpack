# Configuration & Environment Reference

Backpack is configured primarily through environment variables. This allows for seamless deployment across Docker, Kubernetes, and bare-metal environments.

## 1. Core Infrastructure (Shared)

These variables are required by almost all backend services (API, Gateway, Processors, and Collectors).

| Variable | Description | Default |
| :--- | :--- | :--- |
| `BP_LOGS` | Absolute path where service logs will be written. | `-` |
| `BP_OTEL_HOST` | Hostname of the OpenTelemetry collector for traces/metrics. | `localhost` |
| `BP_RABBIT_MQ_HOST` | Hostname of the RabbitMQ server. | `localhost` |
| `BP_RABBIT_MQ_USER` | RabbitMQ username. | `guest` |
| `BP_RABBIT_MQ_PASS` | RabbitMQ password. | `guest` |

## 2. Database & Cache (API & Gateway)

Required for state management and metadata storage.

| Variable | Description | Default |
| :--- | :--- | :--- |
| `BP_MONGO_STR` | MongoDB connection string (e.g., `mongodb://user:pass@host:27017`). | `-` |
| `BP_REDIS_HOST` | Hostname of the Redis server. | `localhost` |
| `BP_REDIS_USER` | Redis username (if using ACLs). | `-` |
| `BP_REDIS_PASS` | Redis password. | `-` |

## 3. Identity & Access (OIDC)

Configures how Backpack authenticates users and validates tokens.

| Variable | Description | Default |
| :--- | :--- | :--- |
| `OIDC_AUTHORITY` | The URL of your OIDC provider (e.g., Keycloak, Okta). | `http://localhost:8090/realms/master` |
| `OIDC_AUDIENCE` | The expected 'aud' claim in the JWT (Client ID). | `backpack` |

## 4. Storage (Collectors)

Required by all Collector modules to store binaries in S3-compatible storage.

| Variable | Description | Default |
| :--- | :--- | :--- |
| `BP_S3_ENDPOINT` | The URL of the S3 service (e.g., `https://s3.amazonaws.com` or Minio). | `-` |
| `BP_S3_ACCESS_KEY` | S3 Access Key. | `-` |
| `BP_S3_SECRET_KEY` | S3 Secret Key. | `-` |
| `BP_S3_REGION` | S3 Region (e.g., `us-east-1`). | `us-east-1` |
| `BP_S3_BUCKET` | The name of the bucket where artifacts are stored. | `-` |

## 5. Module Specifics

### Collectors
| Variable | Description | Default |
| :--- | :--- | :--- |
| `BP_COLLECTOR_DIRECTORY` | Local scratch space for collectors (e.g., for `git clone`). | `/data/` |
| `BP_COLLECTOR_HTTP_DELTA`| Whether to use delta-mode for HTTP downloads. | `true` |
| `BP_COLLECTOR_HTTP_MODE` | Storage strategy for HTTP (e.g., `lake`). | `lake` |
| `BP_COLLECTOR_CONTAINER_REGISTRY` | The registry host for the Container collector. | `-` |
| `BP_COLLECTOR_DOWNLOAD_TIMEOUT` | Wall-clock budget for one artifact download, as `hh:mm:ss`. Covers the request, the transfer and the upload to storage. | `02:00:00` |
| `BP_COLLECTOR_CONNECT_TIMEOUT` | Time allowed to establish a connection to a remote host, as `hh:mm:ss`. | `00:00:30` |

The download clients themselves have **no** timeout: `HttpClient.Timeout` applies to
reading the response body as well as to the request, so its 100 second default
caps every download at 100 seconds regardless of size, which nothing the size of
a model or an image layer can meet. `BP_COLLECTOR_DOWNLOAD_TIMEOUT` replaces it
with a budget applied per download, and `BP_COLLECTOR_CONNECT_TIMEOUT` keeps an
unreachable host from consuming that budget.

Keep the download budget below the MassTransit consumer timeout in
`Core.Kernel/RegistrationUtils.cs` (180 minutes). Within it, an expired download
is raised as an `ArtifactTimeoutException`, which the receive endpoint retries
and then redelivers after 5, 15 and 30 minutes. Above it the broker abandons the
message first and the artifact is simply lost.

### Frontend (GUI)
These are typically built into the React application or provided via a `.env` file during development.

| Variable | Description | Default |
| :--- | :--- | :--- |
| `VITE_OIDC_AUTHORITY` | Overrides the authority fetched from the backend. | `-` |
| `VITE_OIDC_CLIENT_ID` | Overrides the client ID fetched from the backend. | `-` |

## 6. Health Checks

Every service exposes liveness and readiness over HTTP.

| Variable | Description | Default |
| :--- | :--- | :--- |
| `BP_HEALTH_PORT` | Port the health endpoint listens on. | `8080` |

| Route | Runs | Status |
| :--- | :--- | :--- |
| `/health/live` | Only checks tagged `live`, none of which touch the network. | `200`, or `503` if the process is broken. |
| `/health/ready` | Every dependency check: the broker, plus Mongo, Redis and S3 where the service uses them. | `200`, or `503` if a dependency is unreachable. |
| `/health` | Everything, for humans. | As above. |

All three answer with the same JSON document, naming each check and why it
failed. Dependency checks are capped at 5 seconds each, so a probe answers even
when a backend has stopped responding.

Use `/health/live` for a restart probe and `/health/ready` for a traffic or
readiness probe — never the other way round. Liveness deliberately ignores the
broker and the database: if it did not, a RabbitMQ restart would take down every
worker in the fleet at once instead of leaving them idle until it returned.

```yaml
livenessProbe:
  httpGet: { path: /health/live, port: 8080 }
  periodSeconds: 10
readinessProbe:
  httpGet: { path: /health/ready, port: 8080 }
  periodSeconds: 15
  timeoutSeconds: 10   # above the 5s per-check cap
```

The worker images are built on `mcr.microsoft.com/dotnet/runtime:8.0`, which
ships neither `curl` nor `wget`, so there is no Docker `HEALTHCHECK` directive to
go with this — probe the port from outside the container.

## 7. Port Mapping Reference

Standard ports used by Backpack services:

*   **GUI**: `3000` (Development) / `80` (Production Nginx)
*   **Integration.API**: `8004`
*   **Health endpoint (every service)**: `8080`
*   **RabbitMQ Management**: `15672`
*   **Keycloak (Default)**: `8090`
