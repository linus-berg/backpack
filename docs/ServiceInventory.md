# Service Inventory

Every project in this repository, what it does, and what it needs. Each one has a README in
its directory with the details. Images are published as
`ghcr.io/linus-berg/backpack/<image>` (see the
[Deployment Guide](DeploymentGuide.md#services-and-images)).

## Core services

| Service | Image | Queues consumed | Role |
|---------|-------|-----------------|------|
| Integration.API | `integration/api` | — | REST API for the GUI and scripts (OIDC or API key). Stores artifacts and settings, starts work, serves previews and queue status. |
| Core.Gateway | `core/gateway` | `gateway-ingest-unprocessed`, `gateway-ingest-processed`, `gateway-ingest-processed-raw`, `system-event` | Sends artifacts to processors, stores what changed, routes files, follows dependencies, stores system events. |
| Collector.Router | `collector/router` | `collector-router` | Applies version filters and sends each file to the collector for its URL scheme. |
| Tracker.Scheduler | `tracker/scheduler` | `ReloadSchedules` | Re-tracks processors on Quartz cron schedules. Run one instance. |

## Processors

Each processor consumes `processor-<id>` and `processor-<id>-preview` (concurrency 10).
The ID must match the processor definition in MongoDB.

| ID | Image | Artifact ID example | Files go to | Dependencies | Notes |
|----|-------|---------------------|-------------|--------------|-------|
| `npm` | `processor/npm` | `express`, `@types/node` | Collector.Http | `dependencies`, `peerDependencies` | |
| `nuget` | `processor/nuget` | `Newtonsoft.Json` | Collector.Http | all dependency groups | IDs are case-sensitive in Backpack. |
| `pypi` | `processor/pypi` | `requests` | Collector.Http | `requires_dist` | Skips yanked files and macOS wheels. No telemetry. |
| `container` | `processor/container` | `nginx`, `ghcr.io/owner/image` | Collector.Container | — | Lists tags with `skopeo`. |
| `helm` | `processor/helm` | `bitnami/nginx` (Artifact Hub repo/chart) | Collector.Http (charts), Collector.Container (images) | chart dependencies | Optional `ARTIFACTHUB_API_KEY_ID`/`_SECRET`. |
| `terraform` | `processor/terraform` | `hashicorp/aws` | Collector.Http | — | Providers only, linux/amd64. |
| `huggingface` | `processor/huggingface` | `openai-community/gpt2` | Collector.Huggingface | — | Latest commit only. |
| `github-releases` | `processor/github-releases` | `cli/cli` | Collector.Http | — | Config `files` (`;`-separated regexes) and `include_source`. Optional `GITHUB_API_KEY`. |
| `rancher` | `processor/rancher` | `rancher/rancher` | Collector.Container | — | Mirrors the images in a release's image list (config `file`). Optional `GITHUB_API_KEY`. |
| `jetbrains-ide` | `processor/jetbrains-ide` | `IIU` (product code) | Collector.Http | — | Linux downloads only. |
| `jetbrains-plugins` | `processor/jetbrains-plugins` | `7724` (plugin ID) | Collector.Http | — | |
| `php` | — | `monolog/monolog` | Collector.Http | — | Not built or shipped; known bugs (see its README). |

Direct-collect processors have no service: their definition has `direct_collect: true`,
and the artifact ID is a URL that goes straight to a collector, e.g.
`git://github.com/owner/repo` or `docker-archive://docker.io/library/alpine:3.18`.

## Collectors

| Collector | Image | Queues (concurrency) | Stores in | Needs |
|-----------|-------|----------------------|-----------|-------|
| Collector.Http | `collector/http` | `collector-http`, `collector-https` (10) | S3 | — |
| Collector.Huggingface | `collector/huggingface` | `collector-hf` (10) | S3 | optional `BP_HF_TOKEN` |
| Collector.Git | `collector/git` | `collector-git` (10) | S3 (bundles), local mirror | `git`, a persistent `BP_COLLECTOR_DIRECTORY`, optional `BP_CREDENTIALS_FILE` |
| Collector.Container | `collector/container` | `collector-docker`, `collector-oci` (5) | container registry | `skopeo`, `BP_COLLECTOR_CONTAINER_REGISTRY` |
| Collector.DockerArchive | `collector/docker-archive` | `collector-docker-archive` (1) | S3 (tarballs) | `skopeo`, `BP_COLLECTOR_DIRECTORY` |
| Collector.Wget | `collector/wget` | `collector-wget` (10) | S3 | Known problems: `wget://` locations are not crawled, and large sites can hang. See its README. |
| Collector.Rsync | — | `collector-rsync` (10) | — | Not built or shipped; needs an `rsync-os` binary that no image provides. |

`oci://` locations reach Collector.Container but are treated by `skopeo` as local OCI
layouts, so use `docker://` for registry images.

## Libraries

| Project | Used by | Contents |
|---------|---------|----------|
| Core.Kernel | everything | Models, messages, queue names, configuration, module registration, retry policy, telemetry setup. |
| Core.Services | API, gateway, scheduler | Interfaces for the database, cache and artifact service. |
| Core.Infrastructure | API, gateway, scheduler | MongoDB, Redis and RabbitMQ management implementations, `ArtifactService`. |
| Collector.Kernel | storage collectors | S3 storage, object keys, delta links. |
| Library.Skopeo | Processor.Container, Collector.Container, Collector.DockerArchive | Wrapper around the `skopeo` CLI. |
| Library.Github | Processor.Github.Releases, Processor.Rancher | Lists GitHub releases. |

## Tools

| Project | Image | Purpose |
|---------|-------|---------|
| Backpack.GitUnpack | `git-unpack` | Runs on the receiving side: applies the Git bundles Collector.Git produced to bare repositories. |
| Backpack.Tester | — | Console harness for running a processor or collector in-process during development. |
| Templates/ProcessorTemplate | — | `dotnet new` template for a new processor; see [Processor Development](ProcessorDevelopment.md). |
