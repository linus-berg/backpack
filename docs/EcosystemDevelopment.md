# Ecosystem Development Guide

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

The template generates a business logic class (e.g., `Go.cs`) implementing an interface (e.g., `IGo.cs`).

### Step A: Fetch Metadata
In your `ProcessArtifact` method, fetch the latest metadata for the requested artifact ID.

```csharp
public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    logger.LogInformation("Processing Go module {Id}...", artifact.id);

    // 1. Fetch metadata from the upstream registry
    var versions = await FetchVersionsFromUpstream(artifact.id);
    
    // ... logic continues below
}
```

### Step B: Map Versions & Files
Iterate through the upstream versions and map them to the `Artifact.versions` dictionary. For each version, identify the physical file(s) that need to be collected.

```csharp
foreach (var v in versions) {
    var artifactVersion = new ArtifactVersion { version = v };
    
    // Add the physical file URI. The Gateway will route this to a Collector.
    artifactVersion.AddFile(
        name: $"{artifact.id}-{v}.zip", 
        uri: $"{_baseUrl}/{artifact.id}/@v/{v}.zip"
    );

    artifact.AddVersion(artifactVersion);
}
```

### Step C: Identify Dependencies
Parse the manifest for each version to find its dependencies and add them to the `Artifact.dependencies` collection to trigger the recursive loop.

```csharp
var deps = await ResolveDependencies(artifact.id, v);
foreach (var dep in deps) {
    artifact.AddDependency(dep.Id, "go"); // Specify the processor for the dependency
}
```

### Step D: Report Progress
Use the standard logging and event services to report status to the system-wide feed.

---

## 4. Integration & Registration

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
