# Processor.Rancher

Mirrors the container images listed in a GitHub release asset, such as Rancher's
`rancher-images.txt`. For each release that has the configured asset, the processor
downloads the list and reports every line as a container image. Collector.Container copies
those images into a registry. Image: `ghcr.io/linus-berg/backpack/processor/rancher`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-rancher` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-rancher-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

## Artifact ID

The GitHub repository as `<owner>/<repo>`, for example `rancher/rancher`.

## Configuration

| Key | Required | Meaning |
| :--- | :--- | :--- |
| `file` | yes | Exact, case-sensitive name of the release asset that holds the image list, for example `rancher-images.txt`. |

Processor document, as in Compose/mongo-init.js:

```json
{
  "_id": "rancher",
  "config": {
    "file": { "key": "file", "type": "string", "name": "File name in github release", "placeholder": "rancher-images.txt" }
  },
  "direct_collect": false
}
```

## What it reports

Releases come from Library.Github: newest first, drafts and prereleases dropped, stopping
after about 100 releases. For each release whose tag isn't on the artifact yet and that has
an asset named `file`, the processor sends an anonymous `GET` to the asset's
`browser_download_url`.

- **Versions**: `tag_name`. A release without the asset is skipped and not recorded, so it
  is checked again on every run.
- **Files**: one per non-empty line of the list, named after the line as written. The URI is
  `docker://<line>` if the line contains `docker.io`, otherwise `docker://docker.io/<line>`.
- **Where files go**: the files are routed to `collector-docker` (Collector.Container),
  which ignores the module. For example, `rancher/rancher:v2.9.0` is copied to
  `<BP_COLLECTOR_CONTAINER_REGISTRY>/docker.io/rancher/rancher:v2.9.0`. Nothing is stored
  in S3.
- **Dependencies** and **collect_requests**: none.

## Preview

Preview does not work. The preview handler builds the artifact without the request's
`config` (Consumer.cs:23-27), so `artifact.config["file"]` throws. Every preview returns
"The given key 'file' was not present in the dictionary."

## Environment

- The shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
  `BP_OTEL_HOST`.
- `GITHUB_API_KEY` (optional): used only for the release listing (see
  Library.Github/README.md). The asset download is always anonymous.

## Failure behaviour

All releases are handled in one message. One failure therefore loses the whole run, and no
new versions are recorded. None of these failures is retried, and each faulted message is
discarded:

- **GitHub API errors** are rethrown unchanged by Library.Github.
- **A failed asset download** throws from RestSharp.
- **A null response body** raises `ArtifactMetadataException`.
- **A missing `file` key** throws `KeyNotFoundException`.

## Limitations

- Images from other registries get the wrong prefix if their reference doesn't contain
  `docker.io`. For example, `quay.io/org/img:1` becomes `docker://docker.io/quay.io/org/img:1`.
- Lines are split on `\n` only and are not trimmed.
- Collector.Container skips images that already exist in the target registry.
