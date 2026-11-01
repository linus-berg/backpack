# Core.Kernel

Shared library referenced by every Backpack service. It contains the message contracts, the
data models, environment-variable configuration, queue names, and the MassTransit and
OpenTelemetry setup that processors and collectors use. It is not a service and has no
executable.

## Configuration

`Configuration.GetBackpackVariable(CoreVariables.X)` reads the environment variable named like
the enum member. If the variable is unset it returns the default from `Configuration.cs`; if
there is no default it throws `NoNullAllowedException`. A variable set to an empty string
returns the empty string, not the default.

| Variable | Default | Meaning |
| :--- | :--- | :--- |
| `BP_RABBIT_MQ_HOST` | `localhost` | RabbitMQ host. Always vhost `/`. |
| `BP_RABBIT_MQ_USER` | `guest` | RabbitMQ user (AMQP and management API). |
| `BP_RABBIT_MQ_PASS` | `guest` | RabbitMQ password. |
| `BP_RABBIT_MQ_API` | `http://localhost:15672` | RabbitMQ management API. |
| `BP_REDIS_HOST` | `localhost` | Redis endpoint (`host` or `host:port`). |
| `BP_REDIS_USER` | `default` | Redis user. |
| `BP_REDIS_PASS` | `myverylongpassword` | Redis password. |
| `BP_MONGO_STR` | none | MongoDB connection string. Required where MongoDB is used. |
| `BP_OTEL_HOST` | `http://localhost:4318` | OTLP endpoint, see below. |
| `BP_HEALTH_PORT` | `8080` | Port of the health endpoint, see below. Empty turns it off. |
| `BP_OIDC_AUTHORITY` | `http://localhost:8090/realms/backpack` | OIDC issuer (Integration.API). |
| `BP_OIDC_AUDIENCE` | `backpack` | OIDC client ID (Integration.API). |
| `BP_S3_ENDPOINT`, `BP_S3_ACCESS_KEY`, `BP_S3_SECRET_KEY`, `BP_S3_BUCKET`, `BP_S3_REGION` | none | S3 storage. Required where storage is used. |
| `BP_S3_TRACING` | `false` | S3 request tracing. |
| `BP_COLLECTOR_DIRECTORY` | `/data/` | Local working directory of collectors. |
| `BP_COLLECTOR_HTTP_DELTA` | `true` | Collector.Http delta links. |
| `BP_COLLECTOR_HTTP_MODE` | `lake` | Collector.Http mode. |
| `BP_COLLECTOR_CONTAINER_REGISTRY` | none | Target registry for container collectors. |
| `BP_API_HOST` | `http://localhost:4001` | Declared, but no code reads it. |

`BP_CREDENTIALS_FILE` is not part of `CoreVariables`; `HostCredentials.FromEnvironment` reads it
directly. See `Collector.Git/README.md` for its format.

## Queues

Fixed queues (`Endpoints.cs`):

| Queue | Message | Consumer |
| :--- | :--- | :--- |
| `gateway-ingest-unprocessed` | `ArtifactIngestRequest` | Core.Gateway |
| `gateway-ingest-processed` | `ArtifactProcessedRequest` | Core.Gateway |
| `gateway-ingest-processed-raw` | `ArtifactProcessedRequest` as plain JSON | Core.Gateway |
| `collector-router` | `ArtifactRouteRequest` | Collector.Router |
| `system-event` | `SystemEventMessage` | Core.Gateway |

Module queues come from `ModuleRegistration.AddEndpoint(name, concurrency = 10)` and are named
`<module type in lower case>-<name>`, e.g. `processor-npm`, `processor-npm-preview`,
`collector-git`. `ArtifactCollectRequest.GetCollectorModule(location)` maps a location to
`collector-<URI scheme>` (`git://...` goes to `collector-git`). It throws
`InvalidCollectLocationException` if the location has no `://` or is not an absolute URI.
`TryGetCollectorModule` is the non-throwing variant.

## Messages

| Message | Fields | Sent by, to |
| :--- | :--- | :--- |
| `ArtifactIngestRequest` | `artifact` | API, Scheduler to `gateway-ingest-unprocessed` |
| `ArtifactProcessRequest` | `ctx` (Guid), `artifact` | Gateway to `processor-<id>` |
| `ArtifactProcessedRequest` | `context` (Guid), `artifact`, `collect_requests` | Processor to `gateway-ingest-processed` |
| `ArtifactPreviewRequest` / `ArtifactPreviewResponse` | `id`, `processor`, `config` / `artifact`, `error` | API request client to `processor-<id>-preview` |
| `ArtifactRouteRequest` | `artifact` | Gateway, API to `collector-router` |
| `ArtifactCollectRequest` | `location`, `module`, `force` | Gateway, Router, API to `collector-<scheme>` |
| `ReloadSchedulesRequest` | none | Published by the API, consumed by Tracker.Scheduler |
| `SystemEventMessage` | `source`, `message`, `severity`, `user`, `timestamp` | `EventService.LogEvent` to `system-event` |

`MessageExtension` adds `ConsumeContext.Collect(location, module)` and
`ConsumeContext<ArtifactProcessRequest>.ProcessorReply(artifact)`. `ProcessorReply` sends an
`ArtifactProcessedRequest` that carries the request's `ctx` and has no `collect_requests`.

## Models

`Artifact` has `id`, `processor`, `filter`, `filter_type` (`REGEX` or `SEMVER_RANGE`), `status`,
`root`, `config`, `versions` (version string to `ArtifactVersion`, whose `files` map a name to
`{uri, folder}`), and `dependencies`, a set of `ArtifactDependency` (`id`, `processor`,
`config`). Two dependencies are equal when their `id` is equal; `processor` is ignored. Other
models are `Processor`, `ProcessorAuxiliaryField`, `Schedule`, `PendingArtifact`, `ApiKey`,
`Event`, `NewsPost`, `ArtifactSummary` and `QueueStatus`. The MongoDB driver stores every
property named `id` as `_id`.

## Module registration

Processors and collectors register their single consumer with:

```csharp
ModuleRegistration registration = new(ModuleType.PROCESSOR, typeof(Consumer));
registration.AddEndpoint("npm");          // queue processor-npm, 10 concurrent messages
registration.AddEndpoint("npm-preview");  // queue processor-npm-preview
services.Register(registration);
```

`Register` (`RegistrationUtils.cs`) also registers `IEventService` and the health endpoint
(see [Health checks](#health-checks)). Each endpoint gets an
in-memory outbox (messages sent by the consumer are only delivered once it succeeds), a
180-minute timeout, and this retry policy:

- `ArtifactTimeoutException`: 5 immediate retries, then delayed redelivery after 5, 15 and
  30 minutes.
- `ArtifactMetadataException`: never retried.
- Any other exception: not retried.
- Messages that still fault are discarded (`DiscardFaultedMessages`). No `_error` queues are
  created for module endpoints.

Delayed redelivery uses RabbitMQ delayed delivery, which needs the delayed message exchange
plugin (the `masstransit/rabbitmq` image in `Compose/` includes it).

## Health checks

`services.AddHealthEndpoint()` (`Health/HealthExtensions.cs`) starts `HealthEndpointService`,
a minimal HTTP/1.1 listener on `BP_HEALTH_PORT` (IPv4 and IPv6) for services without a web
server, and returns the `IHealthChecksBuilder` for adding checks. `Register` calls it, so every
processor and collector has it; Core.Gateway and Tracker.Scheduler call it themselves.

- `/healthz/live`: 200 between `ApplicationStarted` and `ApplicationStopping`, otherwise 503.
- `/healthz/ready`: runs the checks tagged `HealthTags.READY` (`"ready"`). 503 if one is
  unhealthy or the MassTransit bus (tag `masstransit`, added by MassTransit) isn't healthy.
- A port that is already taken logs a warning; the service runs without the endpoint.

Add a readiness check with the `ready` tag and a timeout:

```csharp
services.AddHealthEndpoint()
        .AddCheck<MyCheck>("my-dependency", HealthStatus.Unhealthy,
                           new[] { HealthTags.READY }, TimeSpan.FromSeconds(3));
```

`Core.Infrastructure` adds `.AddMongo()` and `.AddRedis()`; `Collector.Kernel`'s `AddStorage`
adds the `s3` check. `HealthReportJson` writes the response body, which Integration.API reuses
for its own `/healthz` endpoints.

## Telemetry and logging

- `AddTelemetry(registration)` exports traces (MassTransit, HttpClient, Redis) and metrics
  (HttpClient, .NET runtime, MassTransit) over OTLP. The service name is the entry assembly
  name and the version is `master`.
- `AddLogging(registration)` (on `IHostBuilder`) replaces all logging providers with
  OpenTelemetry logging. Logs go to OTLP and to the OpenTelemetry console exporter.

Both are skipped only when `BP_OTEL_HOST` is empty. The variable has a default, so telemetry is
on unless you set `BP_OTEL_HOST=""`. The exporter always uses gRPC, but the default URL uses
port 4318, which is the usual OTLP/HTTP port. Point it at the collector's gRPC receiver
(normally 4317).

## Other helpers

- `Bin.Execute` runs a binary through CliWrap and captures stdout and stderr. It returns
  `true` when the exit code equals `success_code`, logs stderr as a warning on failure, and
  accepts extra environment variables for secrets.
- `WrapperExtensions.ExecuteWithResult<T>` deserializes a command's stdout as JSON.
  `ExecuteToConsole` pipes output to the console.
- `HostCredentials` provides per-host credentials from `BP_CREDENTIALS_FILE`.
- `EventService` sends audit events to `system-event`. Core.Gateway stores them in the
  MongoDB collection `backpack-events`.
- `ICollector` and `IProcessor` are the consumer interfaces modules implement.
