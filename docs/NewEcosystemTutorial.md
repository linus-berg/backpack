# Tutorial: Implementing a New Ecosystem Processor

This tutorial walks you through the process of creating a new **Processor** module for a software ecosystem (e.g., Go Modules, Conda, or a custom internal registry).

## 1. Install the Processor Template

Backpack provides a standardized .NET template to scaffold new processors. First, install the template from the root of the repository:

```bash
dotnet new install ./Templates/ProcessorTemplate
```

## 2. Scaffold the New Project

Create a directory for your new processor and run the template. In this example, we'll create a processor for **Go Modules**.

```bash
mkdir Processor.Go
cd Processor.Go

# Scaffold the project
# -n: The name of the processor (PascalCase)
# --ENDPOINT: The message queue endpoint name (lowercase)
# --BASE_URL: The upstream registry URL
dotnet new backpack-processor -n Go --ENDPOINT go --BASE_URL https://proxy.golang.org
```

After scaffolding, add the new `.csproj` to the `Backpack.sln` solution.

## 3. Understand the Components

The template generates three core files:

1.  **`IGo.cs`**: The interface defining the processing logic.
2.  **`Go.cs`**: The implementation where you will fetch metadata and resolve dependencies.
3.  **`Consumer.cs`**: The MassTransit consumer that handles incoming `ArtifactProcessRequest` messages.

## 4. Implement the Processing Logic

Open `Go.cs`. This is where the "Greedy" logic is implemented. Your goal is to populate the `Artifact` object with versions, files, and dependencies.

### Example: Basic Implementation

```csharp
public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    logger.LogInformation("Processing Go module {Id}...", artifact.id);

    // 1. Fetch metadata from the upstream registry (e.g., https://proxy.golang.org/<module>/@v/list)
    var versions = await FetchVersionsFromUpstream(artifact.id);

    foreach (var v in versions) {
        // 2. Add the version to the artifact
        var artifactVersion = new ArtifactVersion { version = v };
        
        // 3. Add the physical files that need to be collected
        // The Gateway will route these to the appropriate Collector (HTTP, Git, etc.)
        artifactVersion.AddFile(
            name: $"{artifact.id}-{v}.zip", 
            uri: $"{_baseUrl}/{artifact.id}/@v/{v}.zip"
        );

        artifact.AddVersion(artifactVersion);

        // 4. Resolve and add dependencies for this version
        // This triggers the recursive "Greedy" collection logic
        var deps = await ResolveDependencies(artifact.id, v);
        foreach (var dep in deps) {
            artifact.AddDependency(dep.Id, "go");
        }
    }

    return artifact;
}
```

## 5. How the Ingestion Lifecycle Works

1.  **Request**: The `Core.Gateway` sends an `ArtifactProcessRequest` to your processor's endpoint (`go`).
2.  **Resolution**: Your `Go.cs` logic fetches metadata and identifies all versions, files, and dependencies.
3.  **Reply**: The `Consumer.cs` calls `context.ProcessorReply(artifact)`, sending the populated object back to the Gateway.
4.  **Collection**: The Gateway inspects the `files` in each `ArtifactVersion` and sends `ArtifactCollectRequest` messages to the specialized **Collectors** (e.g., `Collector.Http`).
5.  **Recursion**: The Gateway inspects the `dependencies` and triggers new `ArtifactProcessRequest` messages for those artifacts, continuing until the entire tree is mirrored.

## 6. Registration

To make the system aware of your new processor:
1.  **Deployment**: Deploy your new microservice container.
2.  **Configuration**: Register the `go` endpoint in the Backpack Web GUI or via the `Integration.API`.

## 7. Best Practices

-   **Atomic Operations**: Use the `Artifact` model's helper methods (`AddVersion`, `AddFile`, `AddDependency`) to maintain consistency.
-   **Logging**: Use the provided `ILogger` for structured logging. These logs are automatically captured by OpenTelemetry.
-   - **Error Handling**: Throwing an exception in the logic will trigger MassTransit's retry policy. Ensure your metadata fetching is resilient.
