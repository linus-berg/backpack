# Deployment Guide

Backpack is a set of independent services that talk through RabbitMQ. A deployment is the
infrastructure below, the core services, and one processor per ecosystem plus the
collectors their files need.

## Infrastructure

| Component | Used by | Notes |
|-----------|---------|-------|
| RabbitMQ | every service | Needs the **management plugin** (Status page, queue purge) and the **delayed message exchange plugin** (delayed redelivery of timed-out messages). The `masstransit/rabbitmq` image has both. Queues are created by the services; vhost `/`. |
| MongoDB | API, gateway, scheduler | Database `backpack`, created on first write. |
| Redis | API, gateway, scheduler | Holds the per-ingest dependency context; nothing in it needs a backup. |
| S3-compatible storage | collectors | MinIO, SeaweedFS, Ceph, AWS S3, ... The bucket is created on first use if the key may do so. |
| Container registry | Collector.Container | Only if you mirror container images as images: they are copied into this registry (`BP_COLLECTOR_CONTAINER_REGISTRY`). Collector.DockerArchive stores images as tarballs in S3 instead. |
| OIDC provider | API, GUI | Keycloak or any provider that can put roles in the token; see [Sign-in](#sign-in-oidc). |
| OpenTelemetry Collector | optional | Receives traces, metrics and logs over OTLP/gRPC (`BP_OTEL_HOST`). |

Processors and collectors need outbound access to the registries they mirror, directly or
through `HTTP_PROXY`/`HTTPS_PROXY`/`NO_PROXY`, which the .NET services, `git` and `skopeo`
honour.

## Services and images

CI publishes an image for every service below to
`ghcr.io/linus-berg/backpack/<name>`, tagged `latest` and `<branch>-<commit>` (e.g.
`master-6070fdb…`). Pin the commit tag in production. The GUI is built from
[backpack-gui](https://github.com/linus-berg/backpack-gui) as
`ghcr.io/linus-berg/backpack/gui:<commit>`.

| Image name | Service | Needed |
|------------|---------|--------|
| `integration/api` | Integration.API | always |
| `gui` | GUI | always |
| `core/gateway` | Core.Gateway | always |
| `collector/router` | Collector.Router | always |
| `tracker/scheduler` | Tracker.Scheduler | for scheduled re-tracking |
| `processor/<name>` | one per ecosystem | `npm`, `nuget`, `pypi`, `container`, `helm`, `terraform`, `huggingface`, `github-releases`, `jetbrains-ide`, `jetbrains-plugins`, `rancher` |
| `collector/<name>` | one per URL scheme in use | `http` (http/https), `git`, `container` (docker/oci), `docker-archive`, `huggingface` (hf), `wget` |
| `git-unpack` | Backpack.GitUnpack | optional tool, see its README |

Processor.Php and Collector.Rsync are in the repository but have no published image and
are not usable as they are. Collector.Wget has known problems that stop it from mirroring
anything when used through normal routing; see its README before relying on it.
Which collectors a processor needs is listed in the
[Service Inventory](ServiceInventory.md).

All services are stateless except Collector.Git, which keeps its clones in
`BP_COLLECTOR_DIRECTORY`. Give it a persistent volume, or every restart clones every
repository again. Services can run several replicas, each taking up to the queue's
concurrency limit (10 by default) of messages at a time. The exception is
Tracker.Scheduler: it has no locking, so every replica would fire every schedule. Run
exactly one.

### Verifying images

Images published by CI are signed with [Sigstore](https://www.sigstore.dev/) cosign,
keylessly: the signature is bound to the GitHub Actions workflow that built the image and
recorded in Sigstore's public transparency log. Each image also carries an SPDX SBOM and
SLSA provenance. This applies to images built from the commit that introduced signing
onwards; earlier images are unsigned.

```bash
cosign verify ghcr.io/linus-berg/backpack/core/gateway:master-<commit> \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com \
  --certificate-identity-regexp '^https://github\.com/linus-berg/backpack(-gui)?/\.github/workflows/build-and-publish\.yml@refs/heads/(master|development)$'

# The SBOM and the build provenance:
docker buildx imagetools inspect ghcr.io/linus-berg/backpack/core/gateway:master-<commit> --format '{{ json .SBOM }}'
docker buildx imagetools inspect ghcr.io/linus-berg/backpack/core/gateway:master-<commit> --format '{{ json .Provenance }}'
```

To enforce signatures in a cluster, use an admission policy such as Sigstore's
policy-controller or Kyverno's `verifyImages` with the same issuer and identity. Mirror the
signatures together with the images (`cosign copy`, or a registry that copies
referrers) when you move them into an isolated network.

## Option 1: Kubernetes with Helm

The chart in [linus-berg/helm-charts](https://github.com/linus-berg/helm-charts)
deploys the API, GUI, gateway and router, and the collectors and processors you list. It
does not deploy the infrastructure.

```bash
helm repo add backpack https://linus-berg.github.io/helm-charts/
helm repo update
helm pull backpack/backpack --untar
cp backpack/values.example.yaml my-values.yaml   # then edit it
helm install backpack backpack/backpack -n backpack --create-namespace -f my-values.yaml
```

The chart README documents every value. The essentials: `oidc`, `rabbitmq`, `mongo`,
`redis` and `s3` point at your infrastructure, `collectors` and `processors` list the
workers to run, `credentials` holds tokens for private Git hosts, and `http_proxy`,
`no_proxy` and `ca_certificates` cover restricted networks. Every workload gets startup,
liveness and readiness probes on its [health endpoints](#health-checks); `probes` in the
values tunes or turns them off.

The chart does not deploy Tracker.Scheduler, so schedules don't run in a Helm deployment
unless you run the scheduler yourself.

## Option 2: Docker Compose

[`example/`](../example/README.md) is a single-machine stack with the infrastructure and a
minimal Backpack (npm only). It is meant for evaluation, but shows every setting a
Compose deployment needs. `Compose/` is the development infrastructure only, without any
Backpack services; see [Development Setup](DevelopmentSetup.md).

## Sign-in (OIDC)

The GUI signs users in with the authorization code flow (PKCE) as a **public** client, and
sends the access token to the API.

1. Create a public OpenID Connect client, e.g. `backpack`, with the GUI's URL as redirect
   URI and web origin.
2. Set `BP_OIDC_AUTHORITY` to the issuer URL (Keycloak: `https://<host>/realms/<realm>`)
   and `BP_OIDC_AUDIENCE` to the client ID. Browsers and the API both use that URL, so it
   must resolve to the provider from both, and the tokens' `iss` must match it.
3. Give administrators the role `Administrator`, either as a client role of that client
   (Keycloak puts it in `resource_access.<client>.roles`) or in a top-level `roles`
   claim. Every other signed-in user can browse and request artifacts; requests for
   processors with `requires_approval` wait for an administrator.

Scripts and CI can use API keys instead of tokens; administrators create them on the
GUI's API keys page. See `Integration.API/README.md`.

## First start

1. Start the infrastructure, then the services; they create their queues and the bucket
   themselves.
2. Define the processors you deployed: on the GUI's Processors page (as an
   administrator), or by seeding `backpack-processors` as `example/mongo-init.js` does.
   The `_id` must match the processor's endpoint, e.g. `npm` for `processor-npm`.
   [Processor definitions](Configuration.md#processor-definitions) lists the fields.
3. Add an artifact and follow it on the Status page.

## Health checks

Every service answers two endpoints for Kubernetes probes and monitoring. Integration.API
serves them on its HTTP port. The other services have no web server, so they serve them
on `BP_HEALTH_PORT` (8080 by default) with a minimal built-in HTTP listener.

| Endpoint | Answers | Use for |
| :--- | :--- | :--- |
| `GET /healthz/live` | 200 once the service has started, 503 while it starts or shuts down. Checks no dependencies, so an outage of e.g. MongoDB doesn't restart every pod. | Startup and liveness probes |
| `GET /healthz/ready` | 200 when the service can reach what it needs, 503 when it can't. The JSON body lists every check with its status, description and duration. | Readiness probes, monitoring |

What `/healthz/ready` checks:

| Service | Checks |
| :--- | :--- |
| Integration.API | RabbitMQ (the MassTransit bus), MongoDB, Redis; the OIDC provider's discovery document and the RabbitMQ management API, which can only degrade it |
| Core.Gateway, Tracker.Scheduler | RabbitMQ, MongoDB, Redis |
| Collectors that write to S3 (HTTP, Git, Hugging Face, Wget, Docker archive) | RabbitMQ, S3 (the bucket) |
| Collector.Router, the container and rsync collectors, processors | RabbitMQ |

```json
{"status":"Degraded","duration_ms":61,"checks":[
  {"name":"masstransit-bus","status":"Healthy","description":"Ready","duration_ms":12},
  {"name":"s3","status":"Degraded","description":"Bucket backpack doesn’t exist yet; it is created on first write.","duration_ms":42}]}
```

- **Degraded is still ready** (200). The API stays ready without the OIDC provider (API
  keys and tokens it has already validated keep working) and without the management API
  (only the Status page and purging need it). A bucket that doesn't exist yet is
  degraded too, since collectors create it on first write.
- **Workers are only ready while they consume.** While RabbitMQ is unreachable,
  MassTransit reports the bus as degraded and keeps reconnecting; the gateway, router,
  collectors and processors answer 503 until it is back. Liveness stays 200, so the pods
  aren't restarted. The API has no queues of its own, so a RabbitMQ outage shows there as
  a degraded management API.
- Each check times out after 3 seconds and the checks run in parallel, so a probe timeout
  of 5 seconds is enough.
- The body never contains exception details, since the API's endpoints need no
  authentication. Failing checks are logged with the exception by the service.
- When several services run on one machine, as in development, only the first gets port
  8080; the others log `Health endpoint not started on port 8080` and run without it. Give
  each service its own `BP_HEALTH_PORT`, or set it to an empty string to turn the
  endpoint off.

## Security notes

Backpack is usually deployed on a trusted internal network. Things to be aware of:

- The API allows cross-origin requests from any origin and does not check the token's
  `aud` claim; any token from the configured issuer is accepted.
- `RequireHttpsMetadata` is off, so the authority may be plain HTTP. Use HTTPS for both
  the provider and the API outside development.
- API keys are stored in plain text in MongoDB (`backpack-api-keys`). Anyone who can read
  that collection can use them. Administrators manage keys and purge queues with an OIDC
  token; API keys are not accepted on those endpoints.
- A few endpoints need no authentication: the OIDC settings, the health endpoints
  (`/healthz/*`), the queue statistics (`/api/status/*`), and `GET /api/container/container`, a leftover test endpoint that
  starts a collection of `docker.io/nginx:latest` as a Docker archive. Don't expose the
  API beyond the networks that need it.
- Anyone who can publish to RabbitMQ can trigger collections and write processed results.
  Give Backpack its own RabbitMQ user and vhost access, and don't expose AMQP.
- Collectors fetch whatever URL a processor or a direct-collect artifact names. Use
  `requires_approval` for ecosystems where that matters, and an egress proxy if you need
  to restrict destinations.
