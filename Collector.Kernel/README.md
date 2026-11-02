# Collector.Kernel

Shared library for the collectors that write to S3-compatible storage: it builds the S3
client from environment variables and provides `FileSystem`, which derives object keys,
uploads files and writes delta links. Used by Collector.Http, Collector.Huggingface,
Collector.Git, Collector.Wget and Collector.DockerArchive. (`ICollector`, the consumer
interface every collector implements, is in Core.Kernel, not here.)

## Setup

A collector calls `services.AddStorage()` and registers `FileSystem` as a singleton.
`AddStorage` reads the S3 variables below while the host is built, so a missing variable
stops the service at startup with `NoNullAllowedException: <NAME> is null!`.

`AddStorage` also adds the `s3` readiness check (`/healthz/ready`): healthy when the bucket
exists, degraded when it doesn't exist yet (it is created on first write), unhealthy when S3
is unreachable or rejects the credentials.

## Configuration

| Variable                 | Default  | Effect |
|--------------------------|----------|--------|
| `BP_S3_ENDPOINT`         | none     | Required. `https://host:port` uses TLS; `http://host:port` or a bare `host:port` uses plain HTTP. |
| `BP_S3_ACCESS_KEY`       | none     | Required. |
| `BP_S3_SECRET_KEY`       | none     | Required. |
| `BP_S3_BUCKET`           | none     | Required. Created if missing (checked once per process, on the first storage call; concurrent calls wait for that check, and a bucket another replica created at the same moment counts as created). |
| `BP_S3_REGION`           | none     | Required, but may be empty; an empty value is not passed to the client. |
| `BP_S3_TRACING`          | `false`  | Parsed with `bool.Parse`, so anything but `true`/`false` stops the service at startup. `true` prints every S3 request and response body to stdout. |
| `BP_COLLECTOR_DIRECTORY` | `/data/` | Local working directory. `GetModuleDir(name)` returns `<dir>/<name>`. Only Collector.Git and Collector.DockerArchive use it. |

## Object keys

`GetArtifactPath(module, url)` returns `<module>/<host><path>`: the host is lower-cased and
loses its port, the path is percent-decoded, query string and fragment are dropped, and
every `/-/` becomes `/`. Backslashes are turned into `/` on every read and write.

```text
module npm, https://registry.npmjs.org/@babel/core/-/core-7.0.0.tgz
  -> npm/registry.npmjs.org/@babel/core/core-7.0.0.tgz
```

Only Collector.Http uses this function directly. Git, DockerArchive, Wget and
Huggingface build their own keys; see their READMEs.

## Delta links

`CreateDeltaLink(module, url, artifact_path)` writes a small object at
`delta/<module>/<yyyy_MM_dd>/<host><path>` (UTC date of the write, host and path derived
from `url` as above) whose content is the key of the stored artifact (`artifact_path`
if given, otherwise `GetArtifactPath(module, url)`). Listing `delta/<module>/<day>/` shows
what was collected that day. A second write on the same day overwrites the link. No
collector ever deletes links.

```text
delta/npm/2026_10_31/registry.npmjs.org/@babel/core/core-7.0.0.tgz
  content: npm/registry.npmjs.org/@babel/core/core-7.0.0.tgz
```

## Uploads and retries

- `PutFile` and `PutString` run through the `storage-pipeline` Polly policy: any
  exception is retried up to 5 times, 5 seconds apart (6 attempts).
- When S3 fails (unreachable, erroring, rejecting the credentials), every storage call keeps
  trying, every 15 seconds for up to 10 minutes, and logs a warning once a minute. A
  collector whose consumers all wait on storage takes no more messages, so the rest stay in
  its queue until S3 answers again. After 10 minutes the call throws
  `ArtifactStorageException`, which the collector endpoints redeliver every 15 minutes for a
  day. A key or bucket that doesn't exist is not a failure.
- A stream that can't seek (an HTTP response body) is first copied in full to a temp
  file (`Path.GetTempFileName()`, i.e. `TMPDIR`, usually `/tmp`), which is deleted after
  the upload. Collectors therefore need temp space for the largest file they download,
  times the number of downloads running at once. Errors reading the stream (a download
  that ends early) are thrown as they are; errors writing the temp file (a full disk)
  become `ArtifactStorageException`.
- An upload replaces whatever is at the key; the new object appears only once the upload
  has completed.
- `Exists` is false only when S3 reports the key or bucket as missing; connection or
  permission errors are handled as above. `GetFileInfo` returns null on any error.
