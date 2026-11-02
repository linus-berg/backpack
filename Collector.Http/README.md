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
   downloaded (logged at Debug). On a retry of a request whose download already succeeded,
   the delta link is written again, in case recording it was what failed.
3. `GET <location>` with `User-Agent: Backpack/1.0`. Redirects are followed (.NET default),
   except from HTTPS to HTTP.
4. The body is copied to a temp file and uploaded to the key, replacing any existing
   object. Success is logged with the size and duration.
5. If `BP_COLLECTOR_HTTP_DELTA` is `true`, the collector checks that the object is in the
   bucket and writes a delta link for it. If either fails, the request is retried, so a
   stored file is never left out of transfers.

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

Failures that may pass are thrown as an exception that says what failed, and the endpoint
retries each kind its own way (see `Core.Kernel/RegistrationUtils.cs`). Refusals that won't
change are logged as a warning (`Not collected: <url> answered 404 NotFound…`) and the
request ends.

| Situation | Result |
|-----------|--------|
| 404, 403, 410 and other 4xx; a redirect that is not followed (HTTPS to HTTP) | Warning, nothing stored, not retried. |
| A location that isn't a valid URL | Warning, not retried. |
| 425 or 5xx; DNS, connection, TLS or proxy error; a body that ends early ("The response ended prematurely") | `ArtifactUpstreamException`: 5 immediate retries, then redelivery after 5, 15 and 30 minutes. |
| 408; no response headers within 100 s (.NET default) | `ArtifactTimeoutException`: same retries. |
| 429, or 503 with `Retry-After` | `ArtifactRateLimitedException`: no immediate retries (they would hit the limit again), only redelivery after 5, 15 and 30 minutes. The warning includes the server's `Retry-After`. |
| S3 unreachable or erroring (existence check, upload, delta link) | The storage call waits for S3, trying every 15 s for up to 10 minutes. While it does, this request waits; when all 10 slots wait, the collector takes no more messages and the rest stay in the queue. Then `ArtifactStorageException`: redelivery every 15 minutes for a day. |
| The temp disk can't be written (for example it is full) | `ArtifactStorageException`, as above. |

A request that still fails after the last redelivery is discarded; there are no `_error`
queues. MassTransit logs every retry (`R-RETRY`) and the final fault (`R-FAULT`) with the
reason. Delayed redelivery needs RabbitMQ's delayed message exchange plugin.

## Limitations

- Existence is checked by key only. A file that changes at the same URL is not
  downloaded again unless `force` is set, and the Router never sets it.
- Query string and port are not part of the key, so URLs that differ only in those map to
  the same key and only the first one is stored.
- Once headers have arrived, the body download has no time limit.
- The whole body goes to the temp directory before upload: plan temp space for the
  largest file times up to 20 concurrent downloads (10 per queue).
