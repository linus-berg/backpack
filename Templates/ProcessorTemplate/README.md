# Templates/ProcessorTemplate

A `dotnet new` template (short name `backpack-processor`, defined in
`.template.config/template.json`) that scaffolds a processor project that builds and runs as
generated; you fill in `ProcessArtifact`. This README is copied into every generated
project, so replace it there. The full walkthrough is in
[docs/ProcessorDevelopment.md](../../docs/ProcessorDevelopment.md).

## Parameters

| Option | Replaces | Default | Meaning |
| :--- | :--- | :--- | :--- |
| `-n` / `--name` | `_NAME_` in file names and contents (`sourceName`) | output directory name | PascalCase name. Gives project `Processor.<Name>`, namespace `Processor.<Name>`, classes `<Name>` and `I<Name>`. |
| `--ENDPOINT` | `_ENDPOINT_` | `myprocessor` | Endpoint name. The queue is `processor-<ENDPOINT>`, and it must equal the processor id. |
| `--BASE_URL` | `_BASE_URL_` | `https://api.upstream.com` | Base URL of the `RestClient` in `<Name>.cs`. |

`dotnet new backpack-processor --help` shows the option spellings your SDK generates.

## Generated files

| File | Purpose |
| :--- | :--- |
| `Processor.<Name>.csproj` | Worker SDK, net8.0, RestSharp 112.1.0. References Core.Infrastructure and Core.Kernel. |
| `Program.cs` | Registers `processor-<ENDPOINT>` and `processor-<ENDPOINT>-preview` (concurrency 10). Builds the host with `ProcessorHost.Create` (Core.Infrastructure/Services/ProcessorHost.cs): OTLP logging and telemetry, MassTransit, and an idle `HeartbeatWorker`. |
| `Consumer.cs` | `IProcessor`. Passes `ArtifactProcessRequest` to `I<Name>.ProcessArtifact`, then calls `ProcessorReply`. Answers `ArtifactPreviewRequest` by running `ProcessArtifact` on an unsaved artifact (with the request's `config`) and responding with an `ArtifactPreviewResponse` (`artifact` or `error`). Injects `IEventService` but doesn't use it. |
| `I<Name>.cs` | `Task<Artifact> ProcessArtifact(Artifact artifact)`. |
| `<Name>.cs` | Stub with a `RestClient` and a TODO. Returns the artifact unchanged. |

The template generates no Dockerfile, appsettings or launchSettings.

## Adding a processor

1. **Install the template.** From the repository root, run
   `dotnet new install ./Templates/ProcessorTemplate`.
2. **Generate the project.** From the repository root, run
   `dotnet new backpack-processor -n Go -o Processor.Go --ENDPOINT go --BASE_URL https://proxy.golang.org`.
   Pass `-o`: without it, `preferNameDirectory` creates a `Go/` subdirectory. Inside an
   existing `Processor.Go/`, that breaks the `..\Core.*` project references.
3. **Implement `ProcessArtifact`.** The preview handler and the `processor-<id>-preview`
   queue are already generated and reuse it.
   - Add each version with `ArtifactVersion.AddFile(name, uri, folder)`. The URI scheme
     selects the collector (`collector-<scheme>`), and an empty folder stores the file under
     the processor id.
   - Add dependencies with `artifact.AddDependency(id, processor)`. The gateway handles the
     recursion.
   - Throw `ArtifactTimeoutException` for transient errors, which are retried.
   - Throw `ArtifactMetadataException` for permanent errors, which are not retried.
4. **Add the project to the solution.** Run
   `dotnet sln Backpack.sln add Processor.Go/Processor.Go.csproj`.
5. **Add a Dockerfile.** Processor.HuggingFace/Dockerfile copies the Core.Infrastructure and
   Core.Services project files that the template's references need, so start from it. Then
   add the project to the matrix in .github/workflows/build-and-publish.yml.
6. **Register the processor.** Do this as an Administrator. The id must equal `ENDPOINT`.
   - Call `POST /api/Processor` with `processor_id`, `requires_approval`, `multi_add`,
     `is_external` and `preview_enabled`. `preview_enabled` defaults to `false` if omitted.
   - Then call `POST /api/Processor/update` with `processor_id`, `description`,
     `direct_collect`, the flags above, and `config`. `config` is a JSON string mapping each
     config key to `{key, type, name, placeholder, default}`
     (Core.Kernel/Models/ProcessorAuxiliaryField.cs).
   - Alternatively, insert the document into the `backpack-processors` collection of the
     `backpack` MongoDB database, as Compose/mongo-init.js does.
