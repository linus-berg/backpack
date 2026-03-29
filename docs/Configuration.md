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

### Frontend (GUI)
These are typically built into the React application or provided via a `.env` file during development.

| Variable | Description | Default |
| :--- | :--- | :--- |
| `VITE_OIDC_AUTHORITY` | Overrides the authority fetched from the backend. | `-` |
| `VITE_OIDC_CLIENT_ID` | Overrides the client ID fetched from the backend. | `-` |

## 6. Port Mapping Reference

Standard ports used by Backpack services:

*   **GUI**: `3000` (Development) / `80` (Production Nginx)
*   **Integration.API**: `8004`
*   **RabbitMQ Management**: `15672`
*   **Keycloak (Default)**: `8090`
