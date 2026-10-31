# Backpack.GitUnpack

Standalone worker for the receiving side of a transfer. It takes the git bundles produced by
Collector.Git out of an input directory and applies them to bare mirror repositories. It then
runs `git update-server-info` so a plain static web server can serve the mirrors ("dumb HTTP").
It does not use RabbitMQ, MongoDB, Redis or S3, and it does not serve HTTP itself.

Collector.Git writes each bundle (`git bundle create --all`) to the bucket key
`git/<host>/<owner path>/<repository>`, e.g. `git/github.com/linus-berg/ATM.Npm`. Copy the
contents of the `git/` prefix into the input directory, keeping that layout.

## Directory layout

```
$GIT_BUNDLE_INPUT/github.com/linus-berg/ATM.Npm     bundle file waiting to be applied
$GIT_BUNDLE_REPOS/github.com/linus-berg/ATM.Npm/    bare mirror, serve this directory over HTTP
$GIT_BUNDLE_ARCHIVE/github.com/linus-berg/ATM.Npm   last bundle applied
```

The bundle's directory relative to the input directory is the owner, and its file name is
the repository name. Both are used unchanged for the mirror and archive paths.

## Behaviour

The worker runs a pass, waits 2 seconds, and repeats. In each pass it lists every file under
`GIT_BUNDLE_INPUT` recursively and processes them in parallel:

1. Files whose name starts with `__IGNORE__` are skipped.
2. If `<repos>/<owner>/<repository>` does not exist, it runs
   `git clone --mirror <bundle> <repository>` in `<repos>/<owner>`.
3. Otherwise it runs `git bundle verify <bundle>` in the mirror and, if that succeeds,
   `git remote update`.
4. On success it runs `git update-server-info` in the mirror and moves the bundle to
   `<archive>/<owner>/<file name>`, replacing an earlier archived bundle.
5. On failure the error is logged and the bundle stays in the input directory, so it is tried
   again on the next pass.

## Configuration

| Variable | Default | Effect |
| :--- | :--- | :--- |
| `GIT_BUNDLE_INPUT` | none | Directory scanned for bundles. |
| `GIT_BUNDLE_REPOS` | none | Directory holding the bare mirrors. |
| `GIT_BUNDLE_ARCHIVE` | none | Directory that applied bundles are moved to. |

All three are required. The service stops at startup with `InvalidOperationException` if any is
unset. Use absolute paths.

## Running

The image is built from the repository root and installs `git`:

```shell
docker build -f Backpack.GitUnpack/Dockerfile -t backpack-gitunpack .
docker run \
  -e GIT_BUNDLE_INPUT=/git/input -e GIT_BUNDLE_REPOS=/git/repos -e GIT_BUNDLE_ARCHIVE=/git/archive \
  -v ./input:/git/input -v ./repos:/git/repos -v ./archive:/git/archive \
  backpack-gitunpack
```

Locally, `dotnet run --project Backpack.GitUnpack` needs `git` on the `PATH`. Set the three
variables yourself, because the paths in the launch profile are machine-specific.

## Gotchas

- The mirror's `origin` is the path of the first bundle in the input directory, and
  `git remote update` fetches from that path. Later bundles must arrive at the same path with
  the same file name, and the input directory must stay mounted at the same location.
- A file directly in `GIT_BUNDLE_INPUT` (with no owner directory) makes the whole pass fail,
  so no bundle is applied until the file is removed.
- Files are not checked for being complete. Copy a bundle in under a name that starts with
  `__IGNORE__` (or write it elsewhere) and rename it when the copy is done.
- There is no ordering or queue per repository. All bundles of a repository have the same file
  name, so only one can wait in the input directory. Collector.Git bundles contain the full
  history (`--all`, no prerequisites), so the newest bundle is enough to bring a mirror up to
  date.
