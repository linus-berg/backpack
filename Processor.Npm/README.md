# Processor.Npm

Resolves npm packages from the public registry `https://registry.npmjs.org/`. Every
published version becomes an artifact version with its tarball, and the package names in
each version's `dependencies` and `peerDependencies` are reported as further `npm`
artifacts. Image: `ghcr.io/linus-berg/backpack/processor/npm`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-npm` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-npm-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in the `backpack-processors` collection must have `_id: "npm"`.
The gateway sends work to `processor-<processor id>`, and the API sends previews to
`processor-<processor id>-preview`.

## Artifact ID

The package name as published, including any scope: `express`, `@types/node`.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

It makes one request per artifact: `GET https://registry.npmjs.org/<id>/`, which returns the
full package document.

- **Versions**: every key of the document's `versions` object that the artifact doesn't
  have yet. Pre-releases are included and dist-tags are ignored.
- **Files**: one per version, named `tarball`, with the URI from `dist.tarball`. The folder
  is empty, so the storage module is `npm`. Collector.Http stores
  `https://registry.npmjs.org/express/-/express-4.18.2.tgz` as
  `npm/registry.npmjs.org/express/express-4.18.2.tgz`. Collector.Kernel/FileSystem.cs
  removes the `/-/` segment.
- **Dependencies**: names only, from `dependencies` and `peerDependencies` of each version
  added in this run, with processor `npm`. Version ranges are ignored, and
  `devDependencies` and `optionalDependencies` are not followed. The gateway creates
  dependency artifacts with an empty filter, so every version of a dependency is mirrored.
- **collect_requests**: none.

## Preview

The preview handler runs the same lookup on a new artifact (empty filter, no stored
versions, request config ignored). It returns the result in
`ArtifactPreviewResponse.artifact`, or the exception message in `error`. Nothing is sent to
the gateway.

## Environment

Only the shared variables from Core.Kernel/Configuration.cs:

- `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` (defaults `localhost`,
  `guest`, `guest`).
- `BP_OTEL_HOST` (default `http://localhost:4318`). Telemetry is exported unless the
  variable is set to an empty string.

No registry credentials are used.

## Failure behaviour

- A `TimeoutException` from the registry call becomes `ArtifactTimeoutException`. It is
  retried: 5 immediate retries, then redelivery after 5, 15 and 30 minutes
  (Core.Kernel/RegistrationUtils.cs).
- Any other error becomes `ArtifactMetadataException` and is not retried. This includes
  HTTP 404 for an unknown package. The message is discarded and the stored artifact stays
  unchanged.
