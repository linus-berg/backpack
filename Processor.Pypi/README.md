# Processor.Pypi

Resolves Python packages from PyPI's JSON API. Each version with at least one usable
distribution file becomes a version. The requirements in `requires_dist` become further
`pypi` artifacts. Image: `ghcr.io/linus-berg/backpack/processor/pypi`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-pypi` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-pypi-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "pypi"`.

## Artifact ID

The project name as on PyPI, for example `requests`. Names are not normalised, so `Django`
and `django` are separate artifacts.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

1. `GET https://pypi.org/pypi/<id>/json` returns all releases and their files.
2. For each version not on the artifact yet, `GET https://pypi.org/pypi/<id>/<version>/json`
   returns that version's `info.requires_dist`. This is one request per new version.

- **Filtering** (Models/PypiRelease.cs): yanked files are dropped, as are `bdist_wininst`
  installers and every file whose name contains `macos`, which includes all `macosx` wheels.
  A version with no file left is not reported.
- **Versions**: the release keys, which are PEP 440 strings such as `2.31.0` or `2.0.0rc1`.
  Pre-releases are included.
- **Files**: the name is the filename and the URI is the file's `url`. The module is `pypi`,
  so a file is stored as
  `pypi/files.pythonhosted.org/packages/<xx>/<yy>/<hash>/requests-2.31.0-py3-none-any.whl`.
- **Dependencies**: for each new version, the name part of every `requires_dist` entry, with
  processor `pypi`. The name part is the text before the first whitespace or any of
  `(;<>=!~`. Environment markers and extras are not evaluated, so Windows-only requirements
  and `extra == "..."` requirements are followed too.
- **collect_requests**: none.

## Preview

The preview handler runs the same processing on a new artifact (request config ignored). That
includes one metadata request per version, so previewing a package with a long history is
slow.

## Environment

Only `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER` and `BP_RABBIT_MQ_PASS`. Program.cs doesn't
call `AddTelemetry`, so `BP_OTEL_HOST` has no effect and this processor exports no traces or
metrics.

## Failure behaviour

- **Project metadata call**: a `TimeoutException` becomes `ArtifactTimeoutException`, which
  is retried. Any other error, including 404 for an unknown project or a null body, becomes
  `ArtifactMetadataException`, which is not retried.
- **Per-version metadata call**: errors are logged at Debug level and the version is added
  without dependencies. Known versions are skipped on later runs, so those dependencies are
  never picked up.

## Limitations

- `[` is not a separator in requirement names, so `urllib3[socks]>=1.26` produces the
  artifact id `urllib3[socks]`, which PyPI doesn't know.
