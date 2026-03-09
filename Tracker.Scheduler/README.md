# Tracker.Scheduler

Tracker.Scheduler is responsible for scheduling and triggering artifact tracking jobs within the Backpack ecosystem.

## Key Components

- **Quartz.NET Integration**: Uses the Quartz.NET scheduling library to define and manage tracking jobs.
- **Job Configuration**: Reads job schedules and artifact configurations to determine when to check for updates.
- **Artifact Tracking**: Triggers tracking jobs that publish `ArtifactIngestRequest` messages to the gateway.

## Interaction with Other Services

- **Core.Gateway**: Publishes `ArtifactIngestRequest` messages to the gateway's `IngestConsumer`.
- **Integration.API**: May be used to trigger or manage schedules via API endpoints.
- **OpenTelemetry**: Provides observability and telemetry data for scheduling operations.
