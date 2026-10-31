# Collector.Huggingface

Downloads files of Hugging Face model repositories for the `hf://` locations produced by
Processor.HuggingFace and stores them in S3. A file is downloaded again only when its
ETag on huggingface.co differs from the one stored with the object.

## Queue

| Queue          | Concurrency |
|----------------|-------------|
| `collector-hf` | 10          |

## Location format

Processor.HuggingFace emits one location per file of the model's current revision:

```text
hf://<model id>/<file path>?revision=<commit sha>&modelId=<model id>
hf://moonshotai/Kimi-K2-Instruct/config.json?revision=<sha>&modelId=moonshotai/Kimi-K2-Instruct
```

The collector fetches `https://huggingface.co/<modelId>/resolve/<revision>/<file path>`.
Without `revision` it uses `main`; without `modelId` it uses the host plus the first path
segment.

## How a request is handled

1. Key: `<module>/<host><path>` of the location (query string dropped).
2. `HEAD` the resolve URL. A non-2xx response ends the request silently.
3. The remote ETag is the `ETag` header (quotes removed), or `X-Linked-Etag` if there is
   no `ETag`.
4. If the object exists and its `x-hf-etag` metadata equals the remote ETag, the download
   is skipped.
5. Otherwise `GET` the file (non-2xx ends the request silently), copy it to a temp file,
   and upload it with the metadata `x-amz-meta-x-hf-etag`, replacing the old object.
6. If `BP_COLLECTOR_HTTP_DELTA` is `true` and the object exists, a delta link is written.
   This also happens when step 4 skipped the download.

The `force` flag is ignored.

## Storage layout

```text
module   huggingface   (the processor ID; the Router uses it when a file has no folder)
location hf://moonshotai/Kimi-K2-Instruct/config.json?revision=abc&modelId=moonshotai/Kimi-K2-Instruct

huggingface/moonshotai/Kimi-K2-Instruct/config.json
delta/huggingface/2026_10_31/moonshotai/Kimi-K2-Instruct/config.json  (content: the key above)
```

The revision is not part of the key, so a new revision overwrites files in place. Files
that disappear from the model are never deleted from the bucket.

## Configuration

| Variable                  | Default | Effect |
|---------------------------|---------|--------|
| `BP_HF_TOKEN`             | unset   | If set, sent as `Authorization: Bearer <token>` on every request. Needed for gated or private models. |
| `BP_COLLECTOR_HTTP_DELTA` | `true`  | Exactly `true` enables delta links. |

Also required: the S3 variables in [Collector.Kernel](../Collector.Kernel/README.md) and
the shared variables in [docs/Configuration.md](../docs/Configuration.md). Logs go to
the console only.

No external binaries are needed. The image runs as the non-root user `$APP_UID`, which
must be able to write to the temp directory.

## Failure behaviour

| Situation | Result |
|-----------|--------|
| Non-2xx on `HEAD` or `GET` (404, 401 for gated models, 429) | Request completes, nothing stored, nothing logged. |
| `HEAD` response has neither `ETag` nor `X-Linked-Etag` | Exception; message faulted and discarded. |
| Connection error, no headers within 100 s (.NET default) | Message faulted and discarded. |
| S3 error | Retried by the storage policy, then faulted and discarded. |

## Limitations

- .NET lower-cases the host part of the URI. For organisations with capital letters
  (`Qwen/Qwen2-7B`) the key uses the lower-cased name (`huggingface/qwen/Qwen2-7B/...`).
  Worse, the file path is then derived from only the last path segment, so files in
  sub-directories (`figures/logo.png`) are requested at the wrong URL, get a 404 and
  are silently skipped. Top-level files are unaffected.
- A delta link is written on every run, including for unchanged files.
- Files are copied to the temp directory before upload; model weights may need tens of
  GB of temp space per concurrent download.
- Once headers have arrived, the body download has no time limit.
