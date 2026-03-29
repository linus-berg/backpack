# Non-.NET (Raw) Integration Guide

If you are implementing a processor in a language other than C# (e.g., Python, Go, Node.js) and cannot use the MassTransit SDK, Backpack provides a **Raw Ingestion** path. This allows you to interact with the system using standard JSON messages over RabbitMQ.

## 1. High-Level Flow

Standard .NET processors use binary-wrapped MassTransit messages. Raw processors use plain JSON:

1.  **Request**: The Gateway sends an `ArtifactProcessRequest` to your queue (e.g., `processor-python-ecosystem`).
2.  **Processing**: Your service parses the metadata.
3.  **Reply**: Instead of replying to the standard ingest queue, you send a raw JSON `Artifact` object to the **`gateway-ingest-processed-raw`** queue.

## 2. Consuming the Request

Your service should listen on a queue named `processor-<your-name>`. 

Because the Gateway is written in .NET, the incoming message will be wrapped in a **MassTransit JSON Envelope**. You must parse the `message` field to get the actual payload.

### Example Incoming Message
```json
{
  "messageId": "08db0000-5c70-0005-0123-08db23456789",
  "correlationId": "08db0000-5c70-0005-9876-08db23456789",
  "conversationId": "08db0000-5c70-0005-abcd-08db23456789",
  "sourceAddress": "rabbitmq://localhost/gateway",
  "destinationAddress": "rabbitmq://localhost/processor-python-ecosystem",
  "messageType": [
    "urn:message:Core.Kernel.Messages:ArtifactProcessRequest"
  ],
  "message": {
    "artifact": {
      "id": "my-package",
      "processor": "python-ecosystem",
      "filter": ".*",
      "config": {}
    },
    "ctx": "CORRELATION_TOKEN_12345"
  },
  "sentTime": "2023-10-27T10:00:00.000Z",
  "headers": {},
  "host": {
    "machineName": "BACKPACK-GATEWAY",
    "processName": "Core.Gateway",
    "processId": 1234,
    "assembly": "Core.Gateway",
    "assemblyVersion": "1.0.0.0",
    "frameworkVersion": "8.0.0",
    "massTransitVersion": "8.1.0",
    "operatingSystemVersion": "Unix 13.0.0"
  }
}
```

### Key Fields to Extract:
*   **`message.artifact`**: The skeleton artifact object (id, processor, config).
*   **`message.ctx`**: A correlation context string. **Crucial**: You must include this exact string in your reply so the Gateway can track the recursive discovery chain.

## 3. Sending the Reply (The Raw Path)

Once processing is complete, publish a JSON message to the **`gateway-ingest-processed-raw`** exchange/queue. 

### Message Structure
The message must be a JSON object with two top-level fields:

```json
{
  "context": "the-ctx-string-from-the-request",
  "artifact": {
    "id": "my-package",
    "processor": "python-ecosystem",
    "versions": {
      "1.0.0": {
        "version": "1.0.0",
        "files": {
          "package.zip": {
            "uri": "https://upstream.com/package.zip",
            "folder": "optional-subfolder"
          }
        }
      }
    },
    "dependencies": [
      {
        "id": "required-lib",
        "processor": "python-ecosystem"
      }
    ]
  }
}
```

## 4. Why use the Raw Endpoint?

The `gateway-ingest-processed-raw` endpoint in the Gateway is configured with `UseRawJsonDeserializer`. This tells MassTransit to skip its own envelope headers and security checks and simply parse the body as a direct mapping to the `Artifact` class.

## 5. Summary Checklist
1.  Configure your RabbitMQ client to use **Plain JSON** (no MassTransit headers).
2.  Listen on `processor-<name>`.
3.  Extract the `ctx` field from the incoming message.
4.  Perform your logic.
5.  Publish the result to `gateway-ingest-processed-raw`.
6.  Ensure the `context` field in your reply matches the `ctx` from the request.
