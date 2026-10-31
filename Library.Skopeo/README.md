# Library.Skopeo

Small wrapper around the `skopeo` CLI, run through CliWrap. Used by Processor.Container
(listing tags), Collector.Container (copying images to a registry) and
Collector.DockerArchive (copying images to tarballs). It runs `skopeo` from `PATH`; the
image of the service that uses it has to provide the binary.

## SkopeoClient

| Method | skopeo command | Result |
|--------|----------------|--------|
| `GetTags(image)` | `list-tags docker://<image>` | `SkopeoListTagsOutput` (`Repository`, `Tags`), or null on any failure (logged). |
| `ImageExists(location)` | `inspect --tls-verify=false docker://<registry>/<host><path>` | `SkopeoManifest` (only `Name` is parsed), or null on any failure (logged). |
| `CopyToRegistry(location)` | `copy --dest-tls-verify=false <location> docker://<registry>/<host><path>` | `true`. A non-zero exit logs stderr and throws CliWrap's `CommandExecutionException`. |
| `CopyToTar(location, dir, force)` | `inspect --tls-verify=false docker://...`, then `copy docker://<name>:<tag> docker-archive:<tar>:<name>` | `SkopeoArchive` describing the tarball. Throws `SkopeoTagMissingException`, `SkopeoArchiveExistsException`, or `ApplicationException` when no manifest is found. |

`<registry>` is `BP_COLLECTOR_CONTAINER_REGISTRY`. `<host><path>` comes from `Models/Image`:
host and path of the location, percent-decoded, without the port, e.g.
`docker://docker.io/library/nginx:1.27` gives `docker.io/library/nginx:1.27`.

`SkopeoArchive` names the tarball `<name>:<tag>` with `/` replaced by `_`, `:` by
`__COLON__` and `.` by `__DOT__`, plus `.tar`; `tar_with_host` prefixes the registry host
(used as the S3 key by Collector.DockerArchive).

## Configuration

| Variable                          | Default | Effect |
|-----------------------------------|---------|--------|
| `BP_COLLECTOR_CONTAINER_REGISTRY` | none    | Read on each call to `ImageExists` and `CopyToRegistry`; throws `NoNullAllowedException` when unset. Not used by `GetTags` or `CopyToTar`. |

## Behaviour to be aware of

- No call takes a cancellation token or a timeout.
- No credentials are passed; skopeo's default credential lookup applies.
- TLS verification is off for every `inspect` (including the source-registry lookup in
  `CopyToTar`) and for the destination of `CopyToRegistry`.
- `copy` is run without `--all`, so for multi-arch images only the image for the host's
  platform is copied.
- `CopyToTar` does not check the exit code of `skopeo copy`: the failure is logged and the
  archive is returned anyway. Callers have to check that the tarball exists.
- `CopyToTar` takes the tag from the text after the first `:`, which breaks for registries
  with a port and for digest references.
- `CopyToTar` checks only for a local tarball in `dir`; it never looks at S3.
