# Collector.Container

Copies container images from their source registry into a registry you run, using
`skopeo copy`. It does not use S3. For image tarballs in S3, see Collector.DockerArchive.

## Queues

| Queue              | Concurrency |
|--------------------|-------------|
| `collector-docker` | 5           |
| `collector-oci`    | 5           |

Locations look like `docker://docker.io/library/nginx:1.27`, as produced by
Processor.Container (one per tag) and Processor.Rancher.

## How a request is handled

1. The destination is `docker://<BP_COLLECTOR_CONTAINER_REGISTRY>/<host><path>` of the
   location (host lower-cased, port dropped).
2. `skopeo inspect --tls-verify=false <destination>`. If it returns a manifest, the image
   is already there and the request ends.
3. Otherwise `skopeo copy --dest-tls-verify=false <location> <destination>`.

```text
BP_COLLECTOR_CONTAINER_REGISTRY=registry.internal:5000
docker://docker.io/library/nginx:1.27
  -> docker://registry.internal:5000/docker.io/library/nginx:1.27
```

The request's `module` and `force` fields are ignored.

## Configuration

| Variable                          | Default | Effect |
|-----------------------------------|---------|--------|
| `BP_COLLECTOR_CONTAINER_REGISTRY` | none    | Required. `host[:port]` of the target registry. Read on every request: if it is unset the service starts, but every request fails with `NoNullAllowedException`. |

Plus the shared RabbitMQ/OpenTelemetry variables in
[docs/Configuration.md](../docs/Configuration.md). No S3 variables are read. Logs go to
the console only.

## Requirements

- `skopeo` on `PATH`; the image installs Debian's `skopeo` package.
- No registry credentials are passed by the collector. skopeo's own credential lookup
  applies (for example `REGISTRY_AUTH_FILE` or `~/.docker/config.json`), so mount an auth
  file if the source or target registry needs a login.
- TLS is not verified for the target registry (both `inspect` and `copy`), which also
  allows a plain-HTTP registry. The source registry's TLS is verified.

## Failure behaviour

- `skopeo inspect` failing for any reason (image missing, registry down, auth error)
  counts as "not there yet", so a copy is attempted. The error is logged.
- `skopeo copy` exiting non-zero: stderr is logged and the message is faulted and
  discarded. It is not retried.
- skopeo runs without a time limit; it is not given the message's cancellation token.

## Limitations

- Existence is checked per tag. A tag that moves upstream (`latest`) is never copied
  again, and `force` has no effect.
- `skopeo copy` runs without `--all`: for a multi-arch image only the image for the
  collector's own platform is copied (the published image is `linux/amd64`).
- An `oci://` location is passed to skopeo unchanged, and skopeo reads `oci:` as a local
  OCI layout directory, not a registry. Registry images have to use `docker://`.
- The source port is dropped from the destination path, so `myreg:5000/app` and
  `myreg/app` end up at the same destination.
