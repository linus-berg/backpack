# Collector.Router

Turns an artifact's versions into collect requests: for every version that passes the
artifact's filter it sends one `ArtifactCollectRequest` per file to the collector queue
for the file's URL scheme.

## Queue

| Queue              | Message                | Concurrency |
|--------------------|------------------------|-------------|
| `collector-router` | `ArtifactRouteRequest` | 10          |

Route requests are sent by `ArtifactService.Route` (Core.Infrastructure): by Core.Gateway
after a processor has reported an artifact, and when an artifact of a processor without
`direct_collect` is re-validated.

## How a request is handled

1. A version filter is built from the artifact's `filter` and `filter_type`:
   - empty `filter`: every version passes;
   - `SEMVER_RANGE`: `filter` is parsed as a semver range (Semver 2.3,
     `SemVersionRange.Parse`); each version is parsed leniently (`SemVersionStyles.Any`,
     so `v1.2` is read as `1.2.0`);
   - `REGEX` (the default) or anything else: a .NET regular expression, matched with
     `IsMatch`, which is not anchored: `1\.2` also matches `11.2.0`. Use `^...$`.
2. For each passing version and each of its files, it sends
   `{ location: file.uri, module: file.folder or artifact.processor, force: false }` to
   `queue:collector-<scheme of file.uri>`.

```text
processor npm, file https://registry.npmjs.org/lodash/-/lodash-4.17.21.tgz
  -> collector-https, module npm
processor container, file docker://docker.io/library/nginx:1.27
  -> collector-docker, module container
```

## Configuration

Only the shared RabbitMQ/OpenTelemetry variables in
[docs/Configuration.md](../docs/Configuration.md). The Router doesn't access S3 and needs
no external binaries.

## Failure behaviour

Sends go through MassTransit's in-memory outbox and are delivered only if the whole
message is handled without an exception. If anything throws, no request at all is sent
for that artifact, and the route message is discarded (logged by MassTransit). Typical
causes:

- an invalid regular expression or semver range;
- under `SEMVER_RANGE`, a version that isn't semver even leniently (`latest`, a commit
  hash);
- a file URI without `scheme://`.

## Limitations

- `force` is always false, so routed collects never replace files that are already stored.
- Every passing version is sent again on each route; the collectors skip what they
  already have (Collector.Http by key, Collector.Huggingface by ETag, Collector.Container
  by tag; Git, DockerArchive and Wget download again).
- The Router doesn't know which collectors are running. Requests for a scheme without a
  collector wait in `collector-<scheme>` until one consumes them.
