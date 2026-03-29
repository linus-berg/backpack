# Ecosystem Integration Guide

This guide explains how to extend Backpack by implementing a new **Processor**. Processors are the "brains" of the system—they know how to talk to specific registries (NPM, NuGet, Maven, etc.), parse their metadata, and identify dependencies.

## 1. Scaffolding a New Processor

Use the provided .NET template to create the project structure:

```bash
mkdir Processor.MyEcosystem
cd Processor.MyEcosystem
dotnet new backpack-processor -n MyEcosystem --ENDPOINT myecosystem --BASE_URL https://api.upstream.com
```

## 2. Core Concepts

### The Artifact Model
Every item in Backpack is an `Artifact`. Your job is to fill this object with data from the upstream registry.

*   **`id`**: The unique identifier (e.g., `lodash` or `Newtonsoft.Json`).
*   **`processor`**: The name of your module (e.g., `npm`).
*   **`versions`**: A dictionary where keys are version strings and values are `ArtifactVersion` objects.
*   **`dependencies`**: A list of `ArtifactDependency` objects required by this artifact.

### Recursive Discovery (The "Greedy" Nature)
Backpack is designed to be **greedy**. When a processor identifies a dependency, it should not download it. Instead, it should add it to the `dependencies` list. The `Core.Gateway` will automatically detect new dependencies and trigger new processing requests for them, creating a recursive discovery loop until the entire tree is mirrored.

## 3. Implementation Steps

### Step A: Fetch Metadata
In your `ProcessArtifact` method, use `RestSharp` (included in the template) to fetch the latest metadata for the artifact ID.

### Step B: Map Versions
Iterate through the upstream versions and map them to the `Artifact.versions` dictionary.
*   Ensure you capture the remote URL for the actual binary file (tarball, nupkg, etc.).
*   Backpack uses this URL later to trigger the **Collector**.

### Step C: Identify Dependencies
Parse the manifest (e.g., `package.json` or `.nuspec`) to find dependencies. Add them to `Artifact.dependencies`.
*   **Important**: Use the standard ID for the dependency so other processors can recognize it if necessary.

### Step D: Report Progress
Use `IEventService` to log what's happening. This data appears in the "System Events" feed in the GUI.
*   `SUCCESS`: "Successfully parsed metadata for X versions."
*   `WARNING`: "Metadata for version Y is malformed, skipping."
*   `ERROR`: "Upstream registry returned 404."

## 4. Testing & Registration

1.  **Register in GUI**: Go to the "Processor Config" page and add your new processor ID (e.g., `Processor.MyEcosystem`).
2.  **Add a Root**: Use the "Add Artifact" form to request a package.
3.  **Monitor**: Watch the "Status" page to see messages flowing through your new consumer.
