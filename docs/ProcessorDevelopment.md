# Processor Development Guide

A processor teaches Backpack a new ecosystem. Given an artifact ID, it returns every version
of the artifact, the download URL of every file in each version, and the artifact's direct
dependencies. Everything else (deciding what changed, following dependencies, filtering
versions, downloading and storing) is done by the rest of the system.

This guide walks through a new .NET processor built from the template. Processors in other
languages are covered in [Raw Integration](RawIntegration.md).

## What a processor receives and returns

The gateway sends an `ArtifactProcessRequest` to `processor-<id>`, carrying the artifact as
stored in MongoDB. The processor fills in the artifact and replies with
`context.ProcessorReply(artifact)`:

| Fill in | How | Notes |
|---------|-----|-------|
| Versions | `artifact.AddVersion(new ArtifactVersion { version = "1.2.3" })` | Report **all** versions. The artifact's filter is applied later by the router. |
| Files | `version.AddFile(name, uri, folder = "")` | `uri` must be an absolute URL; its scheme picks the collector (`https` → Collector.Http). `folder` sets the storage module; empty means the processor ID. |
| Dependencies | `artifact.AddDependency(id, processor)` | **Direct** dependencies only, usually with your own processor ID. The gateway recurses. |

Per-artifact settings that an administrator defines for the processor (for example a file
pattern) arrive in `artifact.config`. Read them with `TryGetValue` and fall back to a
default, since older artifacts may not have every key.

Then:

1. The gateway compares the reply with the stored artifact. If nothing changed it stops,
   which is why the reply has to be deterministic: report versions and files the same way
   every time.
2. If it changed, the gateway stores it and sends it to Collector.Router, which sends one
   collect request per file of every version that matches the filter.
3. The gateway sends each dependency it hasn't seen in this ingest to its processor.

Processors are stateless: don't check MongoDB or the bucket. Collectors skip files that
are already stored.

## 1. Generate the project

From the repository root:

```bash
dotnet new install ./Templates/ProcessorTemplate
dotnet new backpack-processor -n Go -o Processor.Go --ENDPOINT go --BASE_URL https://proxy.golang.org
dotnet sln Backpack.sln add Processor.Go/Processor.Go.csproj
```

- `-n`: PascalCase name, used for the project, namespace and classes (`Processor.Go`, `Go`,
  `IGo`).
- `--ENDPOINT`: the processor ID. The service consumes `processor-go` and
  `processor-go-preview`, and the processor definition's `_id` must be `go`.
- `--BASE_URL`: base address of the generated `RestClient`.
- `-o`: always pass it. Without it the template creates a nested `Go/` directory, which
  breaks the relative project references.

The generated project builds and runs as is: `Program.cs` registers both queues,
`Consumer.cs` handles processing and previews, and `Go.cs` is the stub you implement.
[`Templates/ProcessorTemplate/README.md`](../Templates/ProcessorTemplate/README.md) lists
every generated file.

## 2. Implement `ProcessArtifact`

`Go.cs`, using the Go module proxy as the upstream. Explicit types, as everywhere in this
repository (see [GEMINI.md](../GEMINI.md)):

```csharp
public async Task<Artifact> ProcessArtifact(Artifact artifact) {
  /* <module>/@v/list returns one version per line. */
  RestResponse list = await client_.ExecuteAsync(
    new RestRequest($"{artifact.id}/@v/list")
  );
  if (list.StatusCode == HttpStatusCode.NotFound) {
    throw new ArtifactMetadataException($"{artifact.id} not found");
  }
  if (!list.IsSuccessful || list.Content == null) {
    /* Network trouble or rate limiting: retried after a delay. */
    throw new ArtifactTimeoutException($"{artifact.id}: {list.StatusCode}");
  }

  foreach (string v in list.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
    ArtifactVersion version = new() {
      version = v
    };
    version.AddFile($"{v}.zip", $"https://proxy.golang.org/{artifact.id}/@v/{v}.zip");
    version.AddFile($"{v}.mod", $"https://proxy.golang.org/{artifact.id}/@v/{v}.mod");
    artifact.AddVersion(version);

    foreach (string dependency in await GetRequirements(artifact.id, v)) {
      artifact.AddDependency(dependency, "go");
    }
  }

  return artifact;
}
```

The files are stored as `go/proxy.golang.org/<module>/@v/<version>.zip` (see
[Storage Architecture](../STORAGE.md)). Module paths with capital letters would need the
proxy's `!`-escaping; real processors handle details like that.

### Errors and retries

The exception you throw decides what happens to the message:

| Exception | Effect |
|-----------|--------|
| `ArtifactTimeoutException` | Retried 5 times immediately, then after 5, 15 and 30 minutes. Use it for timeouts. |
| `ArtifactUpstreamException` | Same retries. Use it when the registry fails in a way that may pass: connection errors, 5xx responses. |
| `ArtifactRateLimitedException` | Retried only after 5, 15 and 30 minutes. Use it for 429 (and 503 with `Retry-After`). |
| `ArtifactMetadataException` | Not retried. Use it when the artifact doesn't exist or its metadata can't be read. |
| anything else | Not retried. |

A message that isn't retried, or runs out of retries, is discarded: no reply is sent, the
artifact keeps its previous state, and the error is only in the processor's log. Throwing
for a single broken version discards the whole artifact; skip the version and log it
instead if the rest is still useful.

### Previews

The GUI's add form calls the generated preview handler, which runs `ProcessArtifact` on an
artifact that isn't stored, with the config the user entered, and shows the versions and
files. Keep `ProcessArtifact` free of side effects so previews stay harmless. If a
preview makes no sense for your ecosystem, set `preview_enabled: false` in the processor
definition.

## 3. Run it locally

Copy a launch profile (`Properties/launchSettings.json`) from another processor, such as
Processor.Npm, so the service connects to the development stack, and start it next to the
API, gateway, router and the collectors your files need:

```bash
dotnet run --project Processor.Go
```

See [Development Setup](DevelopmentSetup.md).

## 4. Register the processor

Add a processor definition with `_id` = your endpoint (`go`): on the GUI's Processors page
as an administrator, through `POST /api/processor` followed by `POST /api/processor/update`,
or by inserting it into `backpack-processors`. Use `config` to define the per-artifact
fields your processor reads; [Processor definitions](Configuration.md#processor-definitions)
lists all fields and flags.

Then add an artifact and follow it on the Status page: `processor-go`, then
`gateway-ingest-processed`, `collector-router` and `collector-https`.

## 5. Build and ship an image

The template doesn't generate a Dockerfile. Copy one from another processor, such as
`Processor.HuggingFace/Dockerfile`, replace the project name, and keep the `COPY` lines for
`Core.Infrastructure`, `Core.Services` and `Core.Kernel`. Add the project to the matrix in
`.github/workflows/build-and-publish.yml` so CI publishes
`ghcr.io/linus-berg/backpack/processor/<name>`, and to the Helm values (`processors`) of
the deployments that need it.

Finally, write the processor's `README.md`: artifact ID format, config keys, upstream
endpoints, files and their storage keys, dependencies, and failure behaviour. The
existing processor READMEs show the layout.
