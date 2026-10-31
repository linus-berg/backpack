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
`no_proxy` and `ca_certificates` cover restricted networks.

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

## Security notes

Backpack is usually deployed on a trusted internal network. Things to be aware of:

- The API allows cross-origin requests from any origin and does not check the token's
  `aud` claim; any token from the configured issuer is accepted.
- `RequireHttpsMetadata` is off, so the authority may be plain HTTP. Use HTTPS for both
  the provider and the API outside development.
- API keys are stored in plain text in MongoDB (`backpack-api-keys`). Anyone who can read
  that collection can use them. Administrators manage keys and purge queues with an OIDC
  token; API keys are not accepted on those endpoints.
- A few endpoints need no authentication: the OIDC settings, the queue statistics
  (`/api/status/*`), and `GET /api/container/container`, a leftover test endpoint that
  starts a collection of `docker.io/nginx:latest` as a Docker archive. Don't expose the
  API beyond the networks that need it.
- Anyone who can publish to RabbitMQ can trigger collections and write processed results.
  Give Backpack its own RabbitMQ user and vhost access, and don't expose AMQP.
- Collectors fetch whatever URL a processor or a direct-collect artifact names. Use
  `requires_approval` for ecosystems where that matters, and an egress proxy if you need
  to restrict destinations.
