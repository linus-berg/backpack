# Troubleshooting & Message Flow

Backpack is a distributed, event-driven system built on RabbitMQ and MassTransit. Understanding the "Life of a Message" is essential for debugging why an artifact might be "stuck" or why a sync failed.

## 1. The Ingestion Pipeline

When you add an artifact via the GUI or a Schedule, the following sequence occurs:

1.  **`Integration.API`**: Publishes an `ArtifactIngestRequest`.
2.  **`Core.Gateway`**: Receives the request. 
    *   Checks if the artifact already exists in MongoDB.
    *   Sends an `ArtifactProcessRequest` to the specific **Processor** queue.
3.  **`Processor.<Type>`**: Receives the request.
    *   Fetches metadata from the upstream registry.
    *   Replies with an `ArtifactProcessedRequest` containing all found versions and dependencies.
4.  **`Core.Gateway`**: Compares the processed results with the current database state.
    *   If new versions are found, it sends an `ArtifactRouteRequest` to the **Router**.
    *   If new dependencies are found, it loops back to step 2 for each dependency.
5.  **`Collector.Router`**: Determines which **Collector** (HTTP, Git, Docker) should handle each file.
6.  **`Collector.<Protocol>`**: Downloads the file and stores it in **S3 Storage**.

## 2. Common Troubleshooting Steps

### Check the "System Events" Feed
The first place to look is the "System Events" log in the Web GUI. 
*   Look for `ERROR` or `WARNING` tags from the specific processor.
*   If a processor cannot reach its upstream registry, it will log an error here.

### Monitor Queue Health
Go to the **Status** page in the GUI to see real-time RabbitMQ metrics.
*   **Backlog**: If the "Ready" count is high and not moving, your worker might be down.
*   **Consumers**: If the "Consumers" count is 0, the service is not running.
*   **Errors**: If messages are moving to `_error` queues, there is a code-level exception occurring.

### Inspect the Database (MongoDB)
Sometimes the UI state might lag. You can verify the raw state in the `processors` or `backpack-events` collections.
*   Check if an artifact is marked as `root: true`.
*   Verify the `versions` dictionary contains the expected entries.

### Check Service Logs
All services write detailed logs to files (usually in the path defined by `BP_LOGS`).
*   The `Core.Gateway` log is the most important for seeing the high-level orchestration.
*   The `Processor` logs will show specific network or parsing errors.

## 3. "Stuck" Artifacts
If an artifact is added but never seems to sync:
1.  Verify the **Scheduler** is running if it was a scheduled job.
2.  Trigger a manual **Re-track** from the Artifact Table. This forces the `Gateway` to re-send the request to the `Processor`.
3.  Check if the **Collector** has access to the S3 bucket. If the collector fails to write to S3, the version will never be marked as "collected."
