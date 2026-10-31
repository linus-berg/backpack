# Processor.Jetbrains.IDE

Mirrors the Linux downloads of JetBrains IDE releases, using JetBrains' product data
service. Image: `ghcr.io/linus-berg/backpack/processor/jetbrains-ide`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-jetbrains-ide` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-jetbrains-ide-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "jetbrains-ide"`.

## Artifact ID

A JetBrains product code. Examples: `IIU` (IntelliJ IDEA Ultimate), `IIC`, `PCP`, `GO`,
`WS`. If the API returns more than one product, only the first is used.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

The processor makes one request:
`GET https://data.services.jetbrains.com/products?code=<id>&release.type=release`. Because
of `release.type=release`, EAP and RC builds are not requested.

- **Versions**: every release that has a `linux` download, keyed by `version` (for example
  `2024.1.4`).
- **Files**: one per version, named `linux`, with the URI from `downloads.linux.link`. The
  folder is empty, so the module is `jetbrains-ide`.
  `https://download.jetbrains.com/idea/ideaIU-2024.1.4.tar.gz` is stored as
  `jetbrains-ide/download.jetbrains.com/idea/ideaIU-2024.1.4.tar.gz`.
- **Not collected**: other download keys (for example `linuxARM64`, `windows`, `mac`) and
  checksum links.
- **Dependencies** and **collect_requests**: none.

## Preview

The preview handler runs the same lookup on a new artifact (request config ignored). It
returns the result, or the exception message in `error`.

## Environment

Only the shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
`BP_OTEL_HOST`. No credentials are used.

## Failure behaviour

- A `TimeoutException` becomes `ArtifactTimeoutException`. It is retried: 5 immediate
  retries, then redelivery after 5, 15 and 30 minutes.
- Every other error becomes `ArtifactMetadataException("<id> metadata error!")` and is not
  retried. This includes an empty product list for an unknown code. The more specific
  "had no products in list" exception is caught by the generic handler and replaced
  (Jetbrains.cs:55-63).
