# Collector.Wget

Collector.Wget is a specialized collector that uses the `wget` CLI tool for retrieving artifacts over HTTP and HTTPS
protocols.

## Key Components

- **WgetConsumer**: Implements `IConsumer<ArtifactCollectRequest>`, handling collection requests for generic resources
  using `wget`.
- **Wget Wrapper**: Handles the execution of the `wget` binary and parses its output for success or failure.
- **Download Management**: Supports various `wget` flags for authentication, timeouts, and mirroring.

## Interaction with Other Services

- **Core.Gateway**: Receives collection requests from the gateway (via `Collector.Router`).
- **OpenTelemetry**: Provides observability and telemetry data for `wget` collection processes.
