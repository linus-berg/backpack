# Collector.Http

Downloads a single file over HTTP or HTTPS and stores it in S3, unless an object already
exists at its key.

## Queues

| Queue             | Concurrency |
|-------------------|-------------|
| `collector-http`  | 10          |
| `collector-https` | 10          |

Requests come from Collector.Router (one per artifact file) or from a direct collect.

## How a request is handled

1. The key is derived from the request's `module` and `location` (see below).
2. If an object exists at the key and `force` is false, the request ends here: nothing is
   downloaded and no delta link is written.
3. `GET <location>` with `User-Agent: Backpack/1.0`. Redirects are followed (.NET default).
   A non-2xx response ends the request without writing or logging anything.
4. The body is copied to a temp file and uploaded to the key, replacing any existing
   object.
5. If `BP_COLLECTOR_HTTP_DELTA` is `true`, the collector checks that the object is in the
   bucket and writes a delta link for it.

## Storage layout

Key: `<module>/<host><path>`, built by `FileSystem.GetArtifactPath`: host lower-cased
without port, path percent-decoded, query string dropped, `/-/` collapsed to `/`.

```text
module   npm
location https://registry.npmjs.org/@babel/core/-/core-7.0.0.tgz

npm/registry.npmjs.org/@babel/core/core-7.0.0.tgz
delta/npm/2026_10_31/registry.npmjs.org/@babel/core/core-7.0.0.tgz  (content: the key above)
```

The Router sets `module` to the file's `folder`, or to the processor ID when that is empty.

## Configuration

| Variable                  | Default | Effect |
|---------------------------|---------|--------|
| `BP_COLLECTOR_HTTP_DELTA` | `true`  | Exactly `true` (case-sensitive) enables delta links. |
| `BP_COLLECTOR_HTTP_MODE`  | `lake`  | Read, but has no effect. |

Also required: the S3 variables in [Collector.Kernel](../Collector.Kernel/README.md), and
the shared RabbitMQ/OpenTelemetry variables in
[docs/Configuration.md](../docs/Configuration.md). Proxies follow the .NET defaults
(`HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY`). Logs go to the console only; this service does
not set up the OTLP log exporter.

No external binaries are needed.

## Failure behaviour

| Situation | Result |
|-----------|--------|
| Non-2xx status (404, 403, 429, 500, ...) | Request completes, nothing stored, nothing logged. |
| DNS, connection or TLS error; no response headers within 100 s (.NET default) | Exception; message faulted and discarded. |
| S3 error during upload | Retried by the storage policy (6 attempts, 5 s apart), then faulted and discarded. |
| S3 unreachable during the existence check | Faulted and discarded (not retried). |

Faulted messages are not retried and don't go to an `_error` queue (see
`Core.Kernel/RegistrationUtils.cs`). MassTransit logs the exception.

## Limitations

- Existence is checked by key only. A file that changes at the same URL is not
  downloaded again unless `force` is set, and the Router never sets it.
- Query string and port are not part of the key, so URLs that differ only in those map to
  the same key and only the first one is stored.
- Once headers have arrived, the body download has no time limit.
- The whole body goes to the temp directory before upload: plan temp space for the
  largest file times up to 20 concurrent downloads (10 per queue).
- A 404 looks like success in the logs; check the bucket if files are missing.
