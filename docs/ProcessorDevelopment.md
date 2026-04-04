# Processor Development Guide

This guide provides a comprehensive overview and step-by-step tutorial for extending the Backpack system by implementing a new **Processor**.

---

## 1. Core Architectural Role

Processors are the ecosystem-specific logic units of Backpack. They are responsible for:
1.  **Metadata Extraction**: Communicating with upstream registries (NPM, NuGet, Maven, etc.) to fetch artifact data.
2.  **Dependency Resolution**: Parsing manifests (e.g., `package.json`, `.nuspec`) to build a dependency graph.
3.  **File Identification**: Mapping remote version strings to specific physical file URIs for collection.

### Exhaustive Discovery Logic
Backpack utilizes an **exhaustive** recursive discovery pattern. When a Processor identifies a dependency, it adds it to the `dependencies` list rather than fetching it directly. The `Core.Gateway` detects these new dependencies and automatically triggers new processing requests, continuing the cycle until the entire dependency tree is mirrored locally.

---

## 2. Scaffolding a New Processor

Backpack provides a standardized .NET template to accelerate development.

### Installation
Run the following command from the root of the repository:
```bash
dotnet new install ./Templates/ProcessorTemplate
```

### Usage
Create a directory for your new processor and run the template (using **Go Modules** as an example):

```bash
mkdir Processor.Go
cd Processor.Go

# -n: The name of the processor (PascalCase)
# --ENDPOINT: The message queue endpoint name (lowercase)
# --BASE_URL: The upstream registry URL
dotnet new backpack-processor -n Go --ENDPOINT go --BASE_URL https://proxy.golang.org
```

After scaffolding, add the new `.csproj` to the `Backpack.sln` solution.

---

## 3. Implementation Steps

The template generates a business logic class (e.g., `Go.cs`) implementing an interface (e.g., `IGo.cs`). Your goal is to populate the `Artifact` object with versions, files, and dependencies.

### Full Implementation Example

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
        // This triggers the recursive Exhaustive collection logic
        var deps = await ResolveDependencies(artifact.id, v);
        foreach (var dep in deps) {
            artifact.AddDependency(dep.Id, "go");
        }
    }

    return artifact;
}
```

---

## 4. The Ingestion Lifecycle

To understand how your code triggers system-wide behavior, follow this lifecycle:

1.  **Request**: The `Core.Gateway` sends an `ArtifactProcessRequest` to your processor's endpoint (`go`).
2.  **Resolution**: Your `Go.cs` logic fetches metadata and identifies all versions, files, and dependencies.
3.  **Reply**: The `Consumer.cs` calls `context.ProcessorReply(artifact)`, sending the populated object back to the Gateway.
4.  **Collection**: The Gateway inspects the `files` in each `ArtifactVersion` and sends `ArtifactCollectRequest` messages to the specialized **Collectors** (e.g., `Collector.Http`).
5.  **Recursion**: The Gateway inspects the `dependencies` and triggers new `ArtifactProcessRequest` messages for those artifacts, continuing until the entire tree is mirrored locally.

---

## 5. Integration & Registration

1.  **Deployment**: Deploy your new microservice container to your infrastructure.
2.  **Web GUI**: Register the new processor endpoint (`go`) in the Backpack Dashboard.
3.  **Trigger**: Use the "Add Artifact" form in the GUI to request a package from the new ecosystem and monitor the "Status" page for message flow.

---

## 5. Implementing in Other Languages

While the .NET template is recommended for native integration, processors can be implemented in any language that supports **RabbitMQ** and **JSON** serialization.

-   **Message Schema**: Refer to the **[Raw Integration Guide](RawIntegration.md)** for technical details on `ArtifactProcessRequest` and `ArtifactProcessedRequest`.
-   **MassTransit Compatibility**: Ensure your JSON message structures and headers are compatible with MassTransit's serialization format.

---

## 6. Best Practices

-   **Atomic Operations**: Use the `Artifact` model's helper methods (`AddVersion`, `AddFile`, `AddDependency`) to maintain consistent state.
-   **Observability**: Leverage the provided `ILogger`. Logs and traces are automatically captured by OpenTelemetry.
-   **Resilience**: Processors are designed to be stateless. If metadata fetching fails, throwing an exception will trigger the system-wide retry policy.
