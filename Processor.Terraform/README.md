# Processor.Terraform

Mirrors Terraform provider builds for linux/amd64 from the public Terraform Registry. It
handles providers only. Modules are not supported, and nothing is parsed from Terraform
configuration. Image: `ghcr.io/linus-berg/backpack/processor/terraform`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-terraform` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-terraform-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "terraform"`.
Compose/mongo-init.js doesn't create it.

## Artifact ID

The provider address `<namespace>/<type>`, for example `hashicorp/aws`.

## Configuration

None. The processor reads no `artifact.config` keys.

## What it reports

1. `GET https://registry.terraform.io/v1/providers/<id>/versions`.
2. For each version not on the artifact yet,
   `GET https://registry.terraform.io/v1/providers/<id>/<version>/download/linux/amd64`.

- **Versions**: the registry's version strings, for example `5.31.0`.
- **Files**: three per version, taken from the download response. The folder is empty, so
  the module is `terraform`.

  | File | Response field |
  | :--- | :--- |
  | `provider_zip` | `download_url` |
  | `provider_sha` | `shasums_url` |
  | `provider_sig` | `shasums_signature_url` |

  For example,
  `https://releases.hashicorp.com/terraform-provider-aws/5.31.0/terraform-provider-aws_5.31.0_linux_amd64.zip`
  is stored as
  `terraform/releases.hashicorp.com/terraform-provider-aws/5.31.0/terraform-provider-aws_5.31.0_linux_amd64.zip`.
- **Dependencies** and **collect_requests**: none.

The `signing_keys` part of the response is ignored. No provider mirror metadata, such as
`index.json` or `<version>.json`, is produced.

## Preview

The preview handler runs the same processing on a new artifact (request config ignored). It
makes one download request per version.

## Environment

Only the shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
`BP_OTEL_HOST`. No credentials are used.

## Failure behaviour

For both requests, a `TimeoutException` becomes `ArtifactTimeoutException`, which is
retried. Any other error becomes `ArtifactMetadataException`, which is not retried.

All versions are handled in one message. If the download request fails for any single
version, for example a version published without a linux/amd64 build, the whole message
fails and no new versions are recorded for the provider.
