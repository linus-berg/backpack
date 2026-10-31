# Raw Integration (processors in other languages)

A processor doesn't have to be .NET. Anything that can read and write JSON over RabbitMQ
can serve an ecosystem: it consumes MassTransit messages from its queue and replies in
plain JSON to a gateway queue that accepts messages without the MassTransit envelope.

## Overview

1. The gateway sends an `ArtifactProcessRequest` to the queue `processor-<id>`, where
   `<id>` is the processor's ID in `backpack-processors` (for example `go`).
2. Your service reads the artifact, fetches its metadata upstream, and fills in versions,
   files and direct dependencies.
3. It publishes an `ArtifactProcessedRequest` as plain JSON to
   `gateway-ingest-processed-raw`.

Everything after that (storing, routing to collectors, recursing into dependencies) is the
same as for .NET processors; see [Troubleshooting](Troubleshooting.md#how-an-artifact-moves-through-the-system).

## Consuming requests

MassTransit sends to `processor-<id>` through a fanout exchange of the same name bound to
a durable queue of the same name, and creates both on first send. If your service may
start first, declare them the same way: durable, not auto-delete.

Messages are MassTransit JSON envelopes (`application/vnd.masstransit+json`). The payload
is in `message`:

```json
{
  "messageId": "0b4e0000-5c70-0005-0123-08dc23456789",
  "conversationId": "0b4e0000-5c70-0005-abcd-08dc23456789",
  "sourceAddress": "rabbitmq://rabbitmq/...",
  "destinationAddress": "rabbitmq://rabbitmq/processor-go",
  "messageType": ["urn:message:Core.Kernel.Messages:ArtifactProcessRequest"],
  "message": {
    "ctx": "5f0c1c62-8d55-4c7e-9a3e-2b6f1d0a9c41",
    "artifact": {
      "id": "golang.org/x/text",
      "processor": "go",
      "filter": "",
      "config": {},
      "versions": {},
      "dependencies": [],
      "root": true
    }
  },
  "sentTime": "2026-10-31T10:00:00Z",
  "headers": {},
  "host": { "machineName": "gateway-7d9f", "processName": "Core.Gateway" }
}
```

- `message.ctx`: the ingest context, a **GUID**. Copy it unchanged into the reply as
  `context`. The gateway uses it to visit every dependency only once per ingest.
- `message.artifact`: the artifact as stored. `config` holds the per-artifact fields
  defined for your processor. `versions` contains what was found last time; you can
  ignore it and return everything again.

The artifact fields are described in the [Type Reference](TypeReference.md).

## Sending the reply

Publish a plain JSON body (content type `application/json`, no MassTransit envelope) to
the exchange `gateway-ingest-processed-raw`, or to the default exchange with that routing
key. The gateway reads it as an `ArtifactProcessedRequest`:

```json
{
  "context": "5f0c1c62-8d55-4c7e-9a3e-2b6f1d0a9c41",
  "artifact": {
    "id": "golang.org/x/text",
    "processor": "go",
    "filter": "",
    "config": {},
    "root": true,
    "versions": {
      "v0.14.0": {
        "version": "v0.14.0",
        "files": {
          "text-v0.14.0.zip": {
            "uri": "https://proxy.golang.org/golang.org/x/text/@v/v0.14.0.zip",
            "folder": ""
          }
        }
      }
    },
    "dependencies": [
      { "id": "golang.org/x/tools", "processor": "go", "config": {} }
    ]
  },
  "collect_requests": []
}
```

Rules:

- Return the artifact you received with `versions` and `dependencies` filled in. Keep
  `id`, `processor`, `filter`, `config` and `root` as they were: the gateway compares the
  whole artifact with the stored one, and a difference counts as a change.
- `versions` maps a version string to its files; `files` maps a file name to its URL.
  The URL's scheme picks the collector (`https` → `collector-https`). `folder` is the
  storage namespace: files are stored under `<folder>/<host><path>`, or
  `<processor id>/...` when it's empty.
- Report every version you find. The artifact's filter is applied later by the router.
- List only **direct** dependencies. The gateway handles recursion. A dependency is
  identified by `id` only; give the `processor` that handles it.
- `collect_requests` (optional): extra files to collect regardless of the filter, as
  `{ "location": "<url>", "module": "<folder>", "force": false }`.

There is no error reply: if the upstream fails, log it and drop the request. The
artifact is tried again the next time it's processed (a re-track, or a change in an
artifact that depends on it).

## Previews

The GUI's add form can ask the processor for a preview through `processor-<id>-preview`,
using MassTransit request/response. A raw processor would have to answer with a
MassTransit envelope sent to the request's `responseAddress`, carrying its `requestId`. If
yours doesn't, set `preview_enabled: false` in its processor definition.

## Raw collectors

Collectors can be written the same way. A collector for the URL scheme `foo` consumes
`collector-foo`; the payload of each envelope is an `ArtifactCollectRequest`
(`{ "location", "module", "force" }`). Nothing is sent back. To keep the storage layout,
write to `<module>/<host><path>` in the bucket and skip existing objects unless `force`
is true; see [Storage Architecture](../STORAGE.md).

## Checklist

1. Add a processor definition with `_id` = your processor ID.
2. Consume `processor-<id>` and read `message.ctx` and `message.artifact` from the
   envelope.
3. Publish `{ "context": <ctx>, "artifact": <filled-in artifact> }` as plain JSON to
   `gateway-ingest-processed-raw`.
4. Make sure collectors exist for the URL schemes of your files.
