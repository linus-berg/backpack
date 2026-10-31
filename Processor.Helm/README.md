# Processor.Helm

Resolves Helm charts through the Artifact Hub API. Each chart version becomes a version that
holds the chart archive and the container images Artifact Hub lists for that version. Chart
dependencies that Artifact Hub maps to a known repository become further `helm` artifacts.
Image: `ghcr.io/linus-berg/backpack/processor/helm`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-helm` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-helm-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "helm"`.

## Artifact ID

`<Artifact Hub repository name>/<chart name>`, as in the package URL
`https://artifacthub.io/packages/helm/<repo>/<chart>`. Example: `bitnami/nginx`.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

1. `GET https://artifacthub.io/api/v1/packages/helm/<id>` returns `available_versions`.
2. For each version not on the artifact yet, `GET .../packages/helm/<id>/<version>`.

A chart with 300 versions takes 301 requests on its first run. Later runs fetch only new
versions.

- **Versions**: the `version` field of the version metadata.
- **`chart` file**: `content_url`, with an empty folder, so the module is `helm`.
  `https://charts.bitnami.com/bitnami/nginx-15.0.0.tgz` is stored as
  `helm/charts.bitnami.com/bitnami/nginx-15.0.0.tgz`.
- **Image files**: one per entry in the version's `containers_images`, named after the image
  reference, with folder `container`. If the reference contains a host (regex `\w+\.\w+/`,
  for example `docker.io/` or `ghcr.io/`), the URI is `docker://<image>`. Otherwise it is
  `docker://docker.io/<image>`. These files go to `collector-docker` (Collector.Container),
  which copies the image to `BP_COLLECTOR_CONTAINER_REGISTRY` and ignores the folder.
- **Dependencies**: for each newly fetched version, every entry in `data.dependencies`
  becomes `<artifacthub_repository_name>/<name>` with processor `helm`.
- **Skipped dependencies**: an entry is skipped if `repository` is empty or a `file://` URI,
  or if `artifacthub_repository_name` is empty. If `repository` isn't an absolute URI (for
  example `@bitnami`), `new Uri()` throws; the exception is logged and the entry is skipped.
- **collect_requests**: none.

## Preview

The preview handler runs the same processing on a new artifact (request config ignored). It
fetches metadata for every version of the chart, so previewing a chart with many versions is
slow.

## Environment

| Variable | Meaning |
| :--- | :--- |
| `ARTIFACTHUB_API_KEY_ID`, `ARTIFACTHUB_API_KEY_SECRET` | Optional. If both are set, they are sent as the `X-API-KEY-ID` and `X-API-KEY-SECRET` headers on every request. Read once, when the `Helm` singleton is created (Helm.cs:29-39). |

The shared `BP_RABBIT_MQ_*` and `BP_OTEL_HOST` variables also apply.

## Failure behaviour

- HTTP errors (404 for an unknown chart, 429 when rate limited), network errors and timeouts
  come out of RestSharp unchanged. They are not mapped to `ArtifactTimeoutException`, so
  they are not retried and the message is discarded.
- All versions are fetched within one message, so a failure part-way loses the whole run.
- An empty chart response raises `ArtifactMetadataException`.
- A version whose metadata comes back empty is skipped.

## Limitations

- Image references whose registry has no dot, such as `localhost:5000/img`, get
  `docker.io/` prepended.
- Only charts and versions that Artifact Hub has indexed are visible.
