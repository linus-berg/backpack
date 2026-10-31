# Processor.Php

**Status: not built or shipped.** The project is not in Backpack.sln and has no entry in
.github/workflows/build-and-publish.yml, so CI neither builds it nor publishes an image. It
has a Dockerfile, but Compose/mongo-init.js creates no processor document for it. The code
also has the bugs listed below.

The processor is meant to resolve Composer packages from Packagist.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-php` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 4 |
| `processor-php-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor id would be `php`.

## Artifact ID

`<vendor>/<package>`, for example `monolog/monolog`.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

The processor makes one request: `GET https://repo.packagist.org/p2/<id>.json` (Composer v2
metadata). This file contains tagged releases only. Dev branches are in `p2/<id>~dev.json`,
which is not fetched.

- **Versions**: the `version` of each entry, for example `3.5.0`.
- **Files**: one per version, named `zipball`, with the URI from `dist.url`. The module is
  `php`. For example, `https://api.github.com/repos/Seldaek/monolog/zipball/<commit>` is
  stored as `php/api.github.com/repos/Seldaek/monolog/zipball/<commit>`.
- **Dependencies**: the intent is `require` plus `require-dev`, minus `php`, with processor
  `php`. Because of the first bug below, only the `require-dev` names are emitted. Platform
  packages (`ext-*`, `lib-*`) are not filtered out, and each fails as its own artifact,
  because Packagist has no metadata for it.
- **collect_requests**: none.

## Known bugs

- **Runtime dependencies are never reported.** `GetRequired()` deserialises `require_dev`
  instead of `require` (Models/PackageVersion.cs:21), so the dev dependencies are added
  twice instead.
- **The wrong field is checked.** `GetRequiredDev()` checks `require.ValueKind` instead of
  `require_dev.ValueKind` (Models/PackageVersion.cs:28).
- **Known versions are never skipped.** `HasVersion(kv.Key)` is passed the package name
  rather than the version (Php.cs:32). `AddVersion` doesn't overwrite, so the result is
  still correct, but dependencies are re-read for every version on every run.
- **Minified metadata is not expanded.** p2 responses are minified: fields that equal the
  previous version's are omitted. As a result, a version that omits `require-dev` adds no
  dependencies.
- **A missing `dist` throws.** An entry without `dist` throws `NullReferenceException` at
  Php.cs:39.

## Preview

The preview handler runs the same lookup on a new artifact (request config ignored). It
returns the result, or the exception message in `error`.

## Environment

Only the shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
`BP_OTEL_HOST`.

## Failure behaviour

A `TimeoutException` becomes `ArtifactTimeoutException`, which is retried. Any other error,
including 404 for an unknown package, becomes `ArtifactMetadataException`, which is not
retried.
