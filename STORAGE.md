# Backpack Storage Architecture

Collectors write everything they collect to one S3-compatible bucket (`BP_S3_BUCKET`), in a
layout derived from the URL each file came from. Collector.Container is the exception: it
copies images into a container registry instead. The shared code is
`Collector.Kernel/FileSystem.cs` and `Collector.Kernel/Storage/Minio/MinioStorage.cs`.

## Object keys

```
<module>/<host><path>
```

- `<module>` is the storage namespace: the processor ID (`npm`, `nuget`, `pypi`, ...) unless
  the processor gave the file another `folder`.
- `<host><path>` come from the file's URL. The scheme, port and query string are dropped,
  the host is lower-cased (as .NET's `Uri` does), and `/-/` is collapsed to `/`.

| Source URL (processor) | Object key |
|------------------------|------------|
| `https://registry.npmjs.org/express/-/express-4.18.2.tgz` (npm) | `npm/registry.npmjs.org/express/express-4.18.2.tgz` |
| `https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg` (nuget) | `nuget/api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg` |
| `https://releases.hashicorp.com/terraform-provider-aws/5.31.0/terraform-provider-aws_5.31.0_linux_amd64.zip` (terraform) | `terraform/releases.hashicorp.com/terraform-provider-aws/5.31.0/terraform-provider-aws_5.31.0_linux_amd64.zip` |
| `hf://openai-community/gpt2/config.json?revision=<sha>` (huggingface) | `huggingface/openai-community/gpt2/config.json` |

So a bucket mirrors the upstream URL structure, grouped by ecosystem, and the same URL
always maps to the same key.

### Collectors with their own layout

| Collector | Key | Notes |
|-----------|-----|-------|
| Collector.Git | `git/<host>/<path>`, e.g. `git/github.com/dotnet/runtime` | A full `git bundle` of the repository (all refs), replaced on every run. Collector.Git keeps the working mirror in `BP_COLLECTOR_DIRECTORY`; Backpack.GitUnpack restores bundles on the other side. |
| Collector.DockerArchive | `docker-archive/<host>/<image:tag, with / → _, : → __COLON__, . → __DOT__>.tar`, e.g. `docker-archive/docker.io/docker__DOT__io_library_alpine__COLON__3__DOT__18.tar` | `docker-archive` tarball made by `skopeo copy`. |
| Collector.Wget | `wget/<host>/<path>` | Directory URLs become `index.html`, pages get `.html`, query strings are folded into the name. |
| Collector.Container | — | No S3: images go to `BP_COLLECTOR_CONTAINER_REGISTRY` as `<registry>/<host>/<path>:<tag>`, e.g. `registry.internal:5000/docker.io/library/nginx:1.27`. |

Each collector's README has the details.

## When a file is downloaded

| Collector | Downloads when |
|-----------|----------------|
| Collector.Http | The key doesn't exist, or the request has `force`. Existing objects are never compared with upstream, so a file that changes upstream under the same URL is not refreshed. |
| Collector.Huggingface | The upstream ETag differs from the `x-hf-etag` metadata stored with the object. |
| Collector.Git | Always: it fetches into its local mirror and uploads a new bundle. |
| Collector.DockerArchive | Always; there is no existence check in S3. |
| Collector.Container | The image isn't in the target registry yet. Moving tags such as `latest` are therefore not refreshed. |

## Writing objects

- **Bucket**: created on first use if it doesn't exist (the key must be allowed to create
  buckets, or create it yourself).
- **No partial objects**: an S3 put either stores the whole object or nothing. Bodies that
  can't be rewound (HTTP downloads) are first buffered in full in the system temp directory,
  so the upload can be retried; give collectors temporary space for the largest file.
- **Retries**: a failed write is retried 5 times, 5 seconds apart. If it still fails, the
  collect request fails and is discarded (see
  [Troubleshooting](docs/Troubleshooting.md#failed-messages-are-discarded)).
- **Concurrency**: two collectors writing the same key store the same content; the last
  write wins.

## Delta links

Every newly collected file also gets a small marker object:

```
delta/<module>/<yyyy_MM_dd>/<host><path>
```

The date is the UTC day of collection, and the object's content is the key of the
collected file. Listing `delta/<module>/<day>/` therefore gives exactly what was added to
that module on that day, which is what an incremental transfer across the air gap needs:

1. List the delta objects for the days since the last transfer.
2. Read each one to get the key of the file it points to.
3. Copy those files, keeping their keys, to the transfer medium.

Delta links are written by Collector.Http and Collector.Huggingface (unless
`BP_COLLECTOR_HTTP_DELTA` is `false`), Collector.Git and Collector.DockerArchive.
Collector.Http writes a link only after confirming the file is in the bucket.
Collector.Huggingface currently also writes links for files that were already up to date,
so its delta folders can list unchanged files. Delta objects are never removed by
Backpack; expire them with a bucket lifecycle rule if you don't need them.

## Consuming the mirror

Backpack doesn't serve what it collects. On the other side of the air gap, the copied
objects are used as they are, or loaded into the tools that serve each ecosystem (a
registry, a package proxy, a Git server), keeping the `<host><path>` structure where the
tool expects upstream URLs.
