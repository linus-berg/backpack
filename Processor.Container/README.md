# Processor.Container

Lists the tags of a container image repository with `skopeo list-tags` and reports each tag
as a version. Each version's only file is the image reference. Collector.Container copies
the images into a registry; nothing is stored in S3. Image:
`ghcr.io/linus-berg/backpack/processor/container`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-container` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-container-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "container"`.

## Artifact ID

An image repository without a tag or digest, in any form skopeo accepts after `docker://`:
`nginx`, `library/nginx`, `docker.io/library/nginx`, `ghcr.io/<owner>/<image>`.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

The processor runs `skopeo list-tags docker://<id>` (Library.Skopeo/SkopeoClient.cs).

- **Versions**: every tag the artifact doesn't have yet, including moving tags such as
  `latest`.
- **Files**: one per tag, named `<id>:<tag>`. The URI is `docker://<Repository>:<tag>`,
  where `Repository` is the normalised name that skopeo prints, for example `nginx` becomes
  `docker.io/library/nginx`. The folder is empty.
- **Where files go**: the `docker` scheme routes each file to `collector-docker`
  (Collector.Container), which ignores the module. Collector.Container copies
  `docker://docker.io/library/nginx:1.27` to
  `docker://<BP_COLLECTOR_CONTAINER_REGISTRY>/docker.io/library/nginx:1.27`, unless that image
  already exists there.
- **Dependencies** and **collect_requests**: none.

## Preview

The preview handler runs the same lookup on a new artifact (request config ignored) and
returns it, or the exception message in `error`. A skopeo failure is not an exception here
(see below), so an unknown image previews as an artifact with no versions, not an error.

## Environment

- The shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
  `BP_OTEL_HOST`.
- `skopeo` must be on `PATH`. The Dockerfile installs it with apt.
- This code passes no registry credentials to skopeo.

## Failure behaviour

If `skopeo list-tags` fails, `SkopeoClient.GetTags` logs the error and returns `null`
(Library.Skopeo/SkopeoClient.cs:131-147). Causes include an unknown repository, missing
authentication, a rate limit or the network. The processor then replies with the artifact
unchanged. No exception is raised, so nothing is retried and nothing is reported as failed.

## Limitations

- Every tag is mirrored unless the artifact filter excludes it. For repositories with many
  tags, set a regex filter such as `^1\.27\.`.
- Collector.Router matches the filter against the tag. With `filter_type` `SEMVER_RANGE`, a
  tag that isn't semver (`latest`, `alpine`) fails to parse and the whole route request
  faults.
- Collector.Container skips images that already exist in the target registry. A moving tag
  such as `latest` is therefore copied once and never updated.
