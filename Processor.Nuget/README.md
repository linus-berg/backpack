# Processor.Nuget

Resolves NuGet packages from nuget.org with the NuGet client SDK (NuGet.Protocol 7.3.1).
Every listed version becomes a version with its `.nupkg`, and the package ids in its
dependency groups become further `nuget` artifacts. Image:
`ghcr.io/linus-berg/backpack/processor/nuget`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-nuget` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-nuget-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "nuget"`.

## Artifact ID

The package id, for example `Newtonsoft.Json`. Backpack compares ids case-sensitively, both
for the MongoDB lookup and for dependency equality, while nuget.org does not.
`Newtonsoft.Json` and `newtonsoft.json` are therefore two artifacts that store the same
files.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

The processor calls `PackageMetadataResource.GetMetadataAsync(id, includePrerelease: true,
includeUnlisted: false)` on the V3 source `https://api.nuget.org/v3/index.json`.

- **Versions**: every listed version, pre-releases included, keyed by
  `Identity.Version.ToString()` (for example `13.0.3`). Unlisted versions are skipped.
- **Files**: one per version, named `nupkg`. The URI is built by the processor rather than
  taken from the metadata: `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.<version>.nupkg`,
  all lower-cased. The module is `nuget`, so the file is stored as
  `nuget/api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg`.
- **Dependencies**: the ids from every dependency group (all target frameworks) of each
  version added in this run, with processor `nuget`. Version ranges are ignored.
- **collect_requests**: none.

## Preview

The preview handler runs the same lookup on a new artifact (request config ignored). It
returns the result, or the exception message in `error`.

## Environment

Only the shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
`BP_OTEL_HOST`. No feed credentials are used, and the source is fixed to nuget.org.

## Failure behaviour

- **Unknown package**: the SDK returns an empty list, so the processor reports no versions
  and no error.
- **SDK errors**: HTTP and protocol errors from the SDK propagate unchanged. They are not
  retried, and the message is discarded.
- **Null result**: a null metadata result raises `ArtifactTimeoutException`, which is retried.
- **Null dependency groups**: null `DependencySets` raises
  `ArtifactMetadataException("No versions found!")`, which is not retried.
