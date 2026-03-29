# Processor Project Template

This template provides a standardized structure for creating new artifact processors in Backpack.

## How to install:

Run the following command from the root of the repository:
```bash
dotnet new install ./Templates/ProcessorTemplate
```

## How to use:

1. Create a new directory for your processor: `mkdir Processor.<YourName>`
2. Run the template: 
```bash
dotnet new backpack-processor -n <YourName> --ENDPOINT <lowercase-name> --BASE_URL <upstream-url>
```
3. Add the new project to the `Backpack.sln` solution.
4. Register the processor in the Backpack Web GUI.

This will automatically handle all file renaming (e.g. `I_NAME_.cs` -> `I<YourName>.cs`) and placeholder replacements.

## Structural Improvements:

- **Simplified Program.cs**: Uses `ProcessorHost.Create` to handle telemetry, MassTransit, and heartbeats in a single call.
- **Uniform Consumer**: Standard implementation that logs events to the system-wide feed automatically.
- **Interface Driven**: Keeps the business logic (`YourName.cs`) separated from the messaging layer.
