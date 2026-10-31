# Collector.DockerArchive

Pulls one image tag from a registry with skopeo, writes it as a `docker-archive` tarball
(the format `docker load` reads) and uploads the tarball to S3.

## Queue

| Queue                      | Concurrency |
|----------------------------|-------------|
| `collector-docker-archive` | 1           |

Locations look like `docker-archive://<registry>/<repository>:<tag>`, e.g.
`docker-archive://docker.io/nginx:1.27`.

## How a request is handled

1. The tag is everything after the first `:` of the location (without the scheme). With
   no `:` a warning is logged and the request ends.
2. `skopeo inspect --tls-verify=false docker://<registry>/<repository>:<tag>` gives the
   image's canonical name, e.g. `docker.io/library/nginx`.
3. If the local tarball (`<BP_COLLECTOR_DIRECTORY>/docker-archive/<encoded>.tar`) already
   exists, it is deleted when `force` is set; otherwise the request ends.
4. `skopeo copy docker://<name>:<tag> docker-archive:<tarball>:<name>`.
5. The tarball is uploaded to S3, a delta link is written, and the local tarball is
   deleted.

There is no check against S3: every request downloads the image again and overwrites
the object. The request's `module` field is ignored.

## Storage layout

`<encoded>` is `<name>:<tag>` with `/` replaced by `_`, `:` by `__COLON__` and `.` by
`__DOT__`. The key is `docker-archive/<registry host>/<encoded>.tar`.

```text
docker-archive://docker.io/nginx:1.27   (canonical name docker.io/library/nginx)

docker-archive/docker.io/docker__DOT__io_library_nginx__COLON__1__DOT__27.tar
delta/docker-archive/2026_10_31/docker.io/docker__DOT__io_library_nginx__COLON__1__DOT__27.tar
```

## Configuration

| Variable                 | Default  | Effect |
|--------------------------|----------|--------|
| `BP_COLLECTOR_DIRECTORY` | `/data/` | Tarballs are written to `<dir>/docker-archive/` before upload. Needs room for the largest image. |

Plus the S3 variables in [Collector.Kernel](../Collector.Kernel/README.md) and the shared
variables in [docs/Configuration.md](../docs/Configuration.md).
`BP_COLLECTOR_CONTAINER_REGISTRY` is not used. Logs go to the console only.

## Requirements

The Dockerfile builds a static skopeo (`SKOPEO_VERSION`, default 1.21.0) from source and
installs `policy.json` as `/etc/containers/policy.json`. That policy accepts every image
without signature verification. No registry credentials are passed by the collector;
skopeo's own credential lookup applies.

## Failure behaviour

- The whole request runs inside the `skopeo-retry` policy: any exception (manifest not
  found, missing tarball, S3 errors after their own retries) is retried up to 20 times,
  2 seconds apart. After that the message is faulted and discarded.
- The exit code of `skopeo copy` is not checked. The error is logged, and success is
  decided only by whether the tarball file exists: no file means a retry; a file that
  skopeo left behind is uploaded.
- A missing tag is logged as a warning and not retried.
- skopeo runs without a time limit.

## Limitations

- With concurrency 1, a failing image holds the queue for its 21 attempts.
- The tag is split at the first `:`, so a registry with a port (`myreg:5000/app:1`) or
  a digest reference (`app@sha256:...`) is parsed wrongly.
- `skopeo inspect` does not verify the source registry's TLS certificate (`copy` does).
- The image name stored in the tarball is `<name>` without a tag, which skopeo turns into
  `<name>:latest`. After `docker load`, re-tag the image if you need the original tag.
- A tarball left in the working directory by a crash makes later requests for that tag
  end at step 3 until `force` is used or the file is deleted.
- Multi-arch images: only the image for the collector's platform is pulled.
