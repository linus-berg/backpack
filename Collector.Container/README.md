# Collector.Container

Collector.Container is responsible for collecting container images from remote registries and storing them as OCI-compliant artifacts.

## Key Components

- **ContainerConsumer**: Implements `IConsumer<ArtifactCollectRequest>`, handling collection requests for container images.
- **Image Collection**: Uses tools like Skopeo or direct registry interaction to pull images and push them to the internal registry or storage.
- **Registry Integration**: Supports authentication with various container registries.

## Interaction with Other Services

- **Core.Gateway**: Receives collection requests from the gateway (via `Collector.Router`).
- **Library.Skopeo**: Uses the Skopeo library to interact with remote registries and inspect images.
- **OpenTelemetry**: Provides observability and telemetry data for image collection processes.
