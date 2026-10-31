# Development Setup

This guide gets a local Backpack running: the infrastructure in containers, the .NET
services from your IDE or the command line, and the GUI from its own repository.

## Prerequisites

- .NET 8 SDK (`global.json` pins 8.0.0 and rolls forward).
- Docker with Compose, or rootless Podman with `docker-compose` (see `Compose/README.md`).
- For the GUI ([backpack-gui](https://github.com/linus-berg/backpack-gui)): Node 20.19+ and
  Yarn 1.
- Only for the services you run outside containers: `git` 2.31+ (Collector.Git) and
  `skopeo` (Collector.Container, Collector.DockerArchive, Processor.Container) on your
  `PATH`.
- An IDE is optional. Every project has launch profiles (`Properties/launchSettings.json`),
  which Rider, Visual Studio and `dotnet run` all use.

## 1. Start the infrastructure

```bash
cd Compose
docker compose up -d
```

| Service | Address | Credentials |
|---------|---------|-------------|
| RabbitMQ (`bp-rabbitmq`) | `localhost:5672`, management UI <http://localhost:15672> | `guest` / `guest` |
| MongoDB (`bp-mongo`) | `localhost:27017` | `root` / see `docker-compose.yml` |
| Mongo Express | <http://localhost:8081> | |
| Redis Stack (`bp-ingest-redis`) | `localhost:6379`, RedisInsight <http://localhost:8001> | `default` / `myverylongpassword` |
| S3, SeaweedFS (`bp-s3`) | `localhost:9000`, admin UI <http://localhost:9001> | `backpack-local` / `backpack-local` |
| Container registry (`bp-internal-registry`) | `localhost:6000` | none; stores images in the `docker-registry` bucket |
| Keycloak (`bp-keycloak`) | <http://localhost:8090> | admin console: `KEYCLOAK_ADMIN` in `docker-compose.yml` |
| Prometheus, OpenTelemetry Collector | <http://localhost:9090> | the collector's OTLP ports aren't published |

Data is kept under `Compose/volumes/`. On first start:

- SeaweedFS creates the buckets `backpack` and `docker-registry`.
- MongoDB runs `Compose/mongo-init.js`, which creates the `backpack` database and seeds
  a few processor definitions.
- Keycloak imports `Compose/backpack-realm.json`: a realm `backpack` with a public client
  `backpack` (redirect URIs and web origins `*`), a client role `Administrator`, and a user
  `admin` that has it. If you don't know that user's password, reset it in the Keycloak
  admin console (realm *backpack* → Users → admin → Credentials).

## 2. Run the backend services

Every service has a launch profile in `<Project>/Properties/launchSettings.json` that points
it at the Compose stack (`localhost`, the credentials above, `BP_S3_ENDPOINT=localhost:9000`,
`BP_COLLECTOR_CONTAINER_REGISTRY=localhost:6000`, and so on). `dotnet run` uses it
automatically:

```bash
dotnet run --project Integration.API     # http://localhost:8004 (Swagger at /swagger)
dotnet run --project Core.Gateway
dotnet run --project Collector.Router
dotnet run --project Processor.Npm       # any processors you need
dotnet run --project Collector.Http      # and the collectors they use
dotnet run --project Tracker.Scheduler   # only for scheduled re-tracking
```

These five are the smallest set that takes an npm package from "added" to "in the bucket".
Anything a variable doesn't override falls back to the defaults in
[Configuration](Configuration.md).

The launch profiles set `BP_OTEL_HOST` to a port nothing listens on, so telemetry export
silently fails. Set it to an empty string to turn it off, or publish the OpenTelemetry
Collector's ports and point it there.

## 3. Run the GUI

```bash
cd ../backpack-gui
yarn install
yarn start        # http://localhost:3000
```

The GUI calls the API at `<same host>:8004/api` and signs in through the OIDC settings it
gets from `GET /api/auth/oidc`. Open it on `localhost` so both resolve.

## 4. Try it

1. Sign in as `admin`.
2. On the Processors page, check that `npm` exists. Its `_id` has to match the queue name
   the processor registers (`processor-npm`).
3. Add an artifact, e.g. `left-pad` for `npm`.
4. Watch the Status page: messages pass through `processor-npm`, `gateway-ingest-processed`,
   `collector-router` and `collector-https`.
5. The files appear in the `backpack` bucket under `npm/registry.npmjs.org/...` (SeaweedFS
   admin UI at <http://localhost:9001>).

If something stops along the way, see [Troubleshooting](Troubleshooting.md).

## Writing a new processor

See the [Processor Development Guide](ProcessorDevelopment.md).
