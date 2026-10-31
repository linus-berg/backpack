# Processor.Jetbrains.Plugins

Mirrors plugin builds from the JetBrains Marketplace. Image:
`ghcr.io/linus-berg/backpack/processor/jetbrains-plugins`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-jetbrains-plugins` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-jetbrains-plugins-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "jetbrains-plugins"`.
Compose/mongo-init.js creates a processor named `jetbrains` instead. Artifacts of that
processor are sent to `processor-jetbrains`, which nothing consumes.

## Artifact ID

The numeric Marketplace plugin id, optionally followed by `-<slug>` as in the plugin URL.
Both `7724` and `7724-docker` work (from `https://plugins.jetbrains.com/plugin/7724-docker`).
Everything from the first `-` onwards is dropped for the API call, but the full string
remains the artifact id.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

The processor makes one request: `GET https://plugins.jetbrains.com/api/plugins/<numeric
id>/updates`. It sends no channel, paging or IDE build parameters, and uses whatever list the
endpoint returns.

- **Versions**: the `version` of each update. Updates that share a version string collapse
  into the first one.
- **Files**: one per version, named `plugin`, with URI
  `https://plugins.jetbrains.com/files/<file>`. Here `<file>` is the update's `file` path,
  for example `7724/<updateId>/<name>.zip`. The folder is empty, so the module is
  `jetbrains-plugins`, and the file is stored as
  `jetbrains-plugins/plugins.jetbrains.com/files/7724/<updateId>/<name>.zip`.
- **Dependencies**: plugin dependencies are not followed.
- **collect_requests**: none.

## Preview

The preview handler runs the same lookup on a new artifact (request config ignored). It
returns the result, or the exception message in `error`.

## Environment

Only the shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
`BP_OTEL_HOST`. No credentials are used.

## Failure behaviour

- A `TimeoutException` becomes `ArtifactTimeoutException`. It is retried: 5 immediate
  retries, then redelivery after 5, 15 and 30 minutes.
- Any other error becomes `ArtifactMetadataException` and is not retried. Examples are an
  unknown id (HTTP 404) or a non-numeric id.
- An empty response body produces no versions and no error.
