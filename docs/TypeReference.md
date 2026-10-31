# Type Reference

The types below are defined in `Core.Kernel` and travel as JSON, in RabbitMQ messages and
in MongoDB documents. Field names are serialized exactly as written (snake_case). In
MongoDB, `id` is stored as `_id`.

## Artifact

`Core.Kernel/Models/Artifact.cs`. Stored in the MongoDB collection named after its
processor.

| Field          | Type | Description |
|----------------|------|-------------|
| `id`           | string | Identifier within the ecosystem, e.g. `lodash`, `Newtonsoft.Json`, `docker.io/library/alpine`. For direct-collect processors, the URL to collect. |
| `processor`    | string | Processor ID, e.g. `npm`. |
| `filter`       | string | Which versions to collect. Empty means all. |
| `filter_type`  | enum `ArtifactFilterType` | `0` = `REGEX` (default; .NET regular expression, unanchored, matched against the version string), `1` = `SEMVER_RANGE` (e.g. `>=4.0.0 <5.0.0`; every version string must be semver, or nothing is routed). |
| `status`       | enum `ArtifactStatus` | Always `0` (`PROCESSING`); not used. |
| `root`         | bool | `true` for artifacts someone added; `false` for artifacts that are only there as dependencies. Re-tracking covers roots only. |
| `versions`     | map string → `ArtifactVersion` | Every version the processor found, keyed by version string. |
| `config`       | map string → string | Values for the processor's extra fields (see [Processor definitions](Configuration.md#processor-definitions)). |
| `dependencies` | array of `ArtifactDependency` | Direct dependencies across all versions. |

The filter is applied by Collector.Router when it routes files, so `versions` always
holds every version, collected or not.

## ArtifactVersion

| Field     | Type | Description |
|-----------|------|-------------|
| `version` | string | Version string, also the key in `versions`. |
| `status`  | enum `ArtifactVersionStatus` | Always `0` (`SENT_FOR_COLLECTION`); not used. |
| `files`   | map string → `ArtifactFile` | Files of this version, keyed by file name. |

## ArtifactFile

| Field    | Type | Description |
|----------|------|-------------|
| `uri`    | string | Absolute URL to download. Its scheme picks the collector (`collector-<scheme>`). |
| `folder` | string | Storage namespace (the `<module>` in `<module>/<host><path>`). Empty means the processor ID. |

Backpack doesn't record whether a file was collected; the object in the bucket is the
record.

## ArtifactDependency

| Field       | Type | Description |
|-------------|------|-------------|
| `id`        | string | ID of the dependency. |
| `processor` | string | Processor that handles it (usually the same one). |
| `config`    | map string → string | `config` for the dependency if it's new. |

Two dependencies are equal when their `id` is equal, regardless of processor.

## Messages

`Core.Kernel/Messages`. .NET services exchange them as MassTransit JSON envelopes; the
payload fields are:

| Message | Fields | Sent by → queue |
|---------|--------|-----------------|
| `ArtifactIngestRequest` | `artifact` | API → `gateway-ingest-unprocessed` |
| `ArtifactProcessRequest` | `ctx` (GUID), `artifact` | gateway → `processor-<id>` |
| `ArtifactProcessedRequest` | `context` (GUID), `artifact`, `collect_requests` (array of `ArtifactCollectRequest`) | processor → `gateway-ingest-processed` (plain JSON: `gateway-ingest-processed-raw`) |
| `ArtifactRouteRequest` | `artifact` | gateway, API → `collector-router` |
| `ArtifactCollectRequest` | `location` (URL), `module` (storage namespace), `force` (bool, default `false`) | router, gateway, API → `collector-<scheme of location>` |
| `ArtifactPreviewRequest` | `id`, `processor`, `config` | API → `processor-<id>-preview` (request/response) |
| `ArtifactPreviewResponse` | `artifact` (or null), `error` (string or null) | processor → API |
| `SystemEventMessage` | `source`, `message`, `severity`, `user`, `timestamp` | API → `system-event` (stored by the gateway) |
| `ReloadSchedulesRequest` | — | API publishes it after schedules change; the scheduler subscribes |

`ArtifactCollectRequest.location` must be an absolute URL with a scheme (`scheme://...`);
anything else is rejected with `InvalidCollectLocationException`.

## Stored documents

All in the MongoDB database `backpack`.

| Collection | Type | Fields |
|------------|------|--------|
| `<processor id>` | `Artifact` | see above |
| `backpack-processors` | `Processor` | see [Processor definitions](Configuration.md#processor-definitions) |
| `pending-approvals` | `PendingArtifact` | `id`, `processor`, `filter`, `config`, `requested_by`, `timestamp` |
| `backpack-schedules` | `Schedule` | `id`, `processor`, `cron`, `last_run`, `next_run` |
| `backpack-events` | `Event` | `id`, `timestamp`, `source`, `message`, `severity` (`INFO`, `WARNING`, `ERROR`, `SUCCESS`), `user` |

API keys (`backpack-api-keys`) and news posts (`backpack-news`) are described in
`Integration.API/README.md`.

## What a processor must do

Whether written in .NET or [another language](RawIntegration.md), a processor:

1. Turns an artifact ID into the list of **all** its versions, with the download URL of
   every file.
2. Lists its **direct** dependencies. The gateway handles recursion.
3. Returns everything it finds. Filtering is done by the router, existence checks by the
   collectors, and change detection by the gateway.
4. Keeps no state of its own and doesn't check storage or MongoDB.
