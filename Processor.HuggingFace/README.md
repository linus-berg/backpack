# Processor.HuggingFace

Mirrors Hugging Face Hub model repositories. The processor reports a single version, the
model's current commit, with one file for each file in the repository. Collector.Huggingface
downloads the files. Image: `ghcr.io/linus-berg/backpack/processor/huggingface`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-huggingface` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-huggingface-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "huggingface"`.
Compose/mongo-init.js doesn't create it.

## Artifact ID

The model id as it appears in the Hub URL, for example `openai-community/gpt2`. Only
models are supported, because the lookup goes to `/api/models/<id>`. Datasets and Spaces are
not.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

The processor sends one unauthenticated request: `GET https://huggingface.co/api/models/<id>`.

- **Versions**: exactly one, keyed by the `sha` the API returns. Existing versions are
  removed first (HuggingFace.cs:51). When the model changes, the previous commit therefore
  disappears from the artifact.
- **Files**: one per entry in `siblings`. The name is `rfilename`, the file's path in the
  repository (for example `config.json` or `onnx/model.onnx`). The URI is
  `hf://<id>/<rfilename>?revision=<sha>&modelId=<id>`. The folder is empty, so the module is
  `huggingface`.
- **Download**: the `hf` scheme routes each file to `collector-hf` (Collector.Huggingface).
  It downloads `https://huggingface.co/<id>/resolve/<sha>/<rfilename>` and stores it at
  `<module>/<URI host><URI path>`, for example
  `huggingface/openai-community/gpt2/config.json`.
- **Dependencies** and **collect_requests**: none.

## Preview

The preview handler runs the same lookup on a new artifact (request config ignored). Errors
don't surface (see below), so an unknown model previews as an artifact with no versions, not
as an error.

## Environment

- The shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
  `BP_OTEL_HOST`.
- The processor sends no token, so private models are not visible to it.
- `BP_HF_TOKEN` is read only by Collector.Huggingface, for downloads.

## Failure behaviour

The metadata call uses RestSharp `ExecuteAsync`, which doesn't throw on HTTP errors or
timeouts. An unknown or private model, rate limiting or a network failure yields no metadata
or an empty `sha`. The artifact is then returned unchanged, with no error. As a result, the
`ArtifactTimeoutException`/`ArtifactMetadataException` handlers in HuggingFace.cs:77-83 are
not reached by HTTP failures.

## Limitations

- **All files are collected.** There is no include or exclude pattern, and the artifact
  filter is matched against the commit sha, not against file names.
- **Semver filters fail.** With `filter_type` `SEMVER_RANGE`, the sha fails to parse in
  Collector.Router and the route request faults.
- **Keys don't include the revision.** A new commit overwrites the stored files under the
  same keys. Collector.Huggingface compares ETags to decide whether to download again.
- **Organisation names are lower-cased in keys.** `System.Uri` lower-cases the URI host, so
  `Qwen/Qwen2.5-7B-Instruct` is stored under
  `huggingface/qwen/Qwen2.5-7B-Instruct/...`.
