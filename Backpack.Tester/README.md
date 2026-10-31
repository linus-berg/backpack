# Backpack.Tester

Console program for trying processors and collectors by hand. It calls their classes directly,
in-process, against the real upstream sources and the configured S3 storage. RabbitMQ, MongoDB,
the Gateway and the API are not involved. It is not an automated test suite: there are no
assertions and no reports, only console output.

## Usage

Edit `Program.cs` and leave exactly one test call uncommented, then run:

```shell
dotnet run --project Backpack.Tester
```

The launch profile (`Properties/launchSettings.json`) sets `BP_S3_*` for a local S3 server on
`localhost:9000` and sets `BP_COLLECTOR_DIRECTORY=./data`. `Program.cs` prints
`No test selected...` at the end even when a test ran.

| Test | What it does | Needs |
| :--- | :--- | :--- |
| `HuggingFaceTest` (active by default) | Processes the model `google/gemma-4-31B-it-assistant` with `Processor.HuggingFace` and downloads every file of every version into storage. Large download. | S3 |
| `PypiTest` | Processes `pandas` with `Processor.Pypi` and prints the version count. | S3 variables (see below) |
| `TerraformTest` | Placeholder; the body is commented out. | S3 variables |
| `GitTest` | Mirrors `git://github.com/linus-berg/ATM.Npm` with `Collector.Git` and uploads the bundle. | S3, `git` binary |
| `SkopeoTest` | Lists the tags of `docker.io/amazon/aws-cli` with `Library.Skopeo`. | S3 variables, `skopeo` binary |
| `MinioTest` | Reads the `BP_S3_*` variables and prints region, endpoint and bucket. Does not connect. | S3 variables |
| `WebMirrorTest` | Mirrors a URL into storage with `Collector.Wget`'s `WebMirror`, with console logging at Debug. Called with a URL, not with the shared provider. | S3 |

## Configuration

`ServiceSetup.Build()` runs before any test that uses the shared service provider and calls
`AddStorage()`. That makes the S3 variables required even for tests that do not touch storage.

| Variable | Default | Used by |
| :--- | :--- | :--- |
| `BP_S3_ENDPOINT`, `BP_S3_ACCESS_KEY`, `BP_S3_SECRET_KEY`, `BP_S3_BUCKET`, `BP_S3_REGION` | none (required) | Storage (`Collector.Kernel`) |
| `BP_S3_TRACING` | `false` | Storage request tracing |
| `BP_COLLECTOR_DIRECTORY` | `/data/` | `FileSystem` working directory (Git clones) |
| `BP_CREDENTIALS_FILE` | none | `HostCredentials` for `GitTest` |
| `GITHUB_API_KEY` | none | Optional token for `Library.Github` |
| `HTTPS_PROXY` | none | Proxy for `Collector.Git` |

The launch profile also sets `BP_MONGO_STR` and `BP_COLLECTOR_CONTAINER_REGISTRY`. None of the
tests read them as written.

## Gotchas

- `ServiceSetup` registers logging without a provider, so log output from the processors and
  collectors is discarded. Only `WebMirrorTest` adds a console logger.
- The Docker image is based on `dotnet/runtime:8.0` without `git` or `skopeo`, so `GitTest` and
  `SkopeoTest` only work outside the container.
- The shared `HttpClient` sends no Hugging Face token. Collector.Huggingface reads
  `BP_HF_TOKEN`, but the tester does not. Files of gated models are not downloaded, and the
  test does not report it, because it ignores the result of `RemoteFile.Get`.
