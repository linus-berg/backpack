# Collector.Git

Mirrors git repositories over HTTPS and uploads a git bundle of each mirror to S3, from
which the repository can be cloned or updated on the offline side.

## Queue and storage

| Queue           | Concurrency |
|-----------------|-------------|
| `collector-git` | 10          |

Locations look like `git://github.com/owner/repo` (a `.git` suffix is optional). The
request's `module` and `force` fields are ignored; every request does the following:

1. Fetch from `https://<host>[:port]/<path>`. An explicit port other than 9418 is kept.
2. The first time, `git clone --mirror` into `<BP_COLLECTOR_DIRECTORY>/git/<host>/<path>`;
   after that, `git remote update --prune` in that mirror. Requests for the same
   repository run one at a time within a process, and stale `*.lock` and `tmp_*` files
   are deleted first. Clone or fetch may take at most 120 minutes.
3. `git bundle create <bundle> --all`, upload the bundle, write a delta link and delete
   the local bundle (created under `<BP_COLLECTOR_DIRECTORY>/git/tmp/bundles/`).

```text
git://github.com/linus-berg/backpack.git

local mirror  /data/git/github.com/linus-berg/backpack
bundle        git/github.com/linus-berg/backpack
delta link    delta/git/2026_10_31/github.com/linus-berg/backpack
```

## Configuration

| Variable                 | Default  | Effect |
|--------------------------|----------|--------|
| `BP_COLLECTOR_DIRECTORY` | `/data/` | Local mirrors and bundles live under `<dir>/git/`. Use a persistent volume, otherwise every restart clones every repository again. |
| `BP_CREDENTIALS_FILE`    | unset    | Per-host credentials for private repositories; see below. |
| `HTTPS_PROXY`            | unset    | Written to git's global config as `http.proxy` when the collector handles its first request. |

Plus the S3 variables in [Collector.Kernel](../Collector.Kernel/README.md) and the shared
variables in [docs/Configuration.md](../docs/Configuration.md). The image installs
Debian's `git` package; credentials need git 2.31 or later.

## Failure behaviour

- Clone or fetch fails (network, authentication, unknown repository, the 120-minute
  limit): git's error output is logged as a warning, followed by one of the errors under
  [Troubleshooting](#troubleshooting). Nothing is uploaded, and the request completes
  without a retry.
- `git bundle create` fails (for example on an empty repository): logged as a warning,
  nothing is uploaded.
- The S3 upload still fails after the storage retries: the message is faulted and
  discarded.

## How to use git bundles

Every bundle contains all refs of the mirror, so each one is complete on its own and
replaces the previous bundle at the same key. The object has no file extension and is
named after the repository. A new bundle and delta link are written on every successful
run, even when nothing changed upstream.

Backpack.GitUnpack automates the steps below. By hand:

```console
# First bundle: clone a bare mirror from it. The bundle's path becomes the
# mirror's remote, so later bundles must be put at the same path.
cp <bundle> /incoming/github.com/linus-berg/backpack
git clone --mirror /incoming/github.com/linus-berg/backpack /srv/git/github.com/linus-berg/backpack

# If serving over http
git -C /srv/git/github.com/linus-berg/backpack update-server-info
```

```console
# Later bundles: replace the file at the same path, then fetch from it.
cp <new bundle> /incoming/github.com/linus-berg/backpack
git -C /srv/git/github.com/linus-berg/backpack bundle verify /incoming/github.com/linus-berg/backpack

# --prune also removes branches and tags that were deleted upstream
git -C /srv/git/github.com/linus-berg/backpack remote update --prune

# If serving over http
git -C /srv/git/github.com/linus-berg/backpack update-server-info
```

## Private repositories (e.g. GitHub Enterprise)

The collector can mirror repositories that need authentication. Credentials
are looked up by host and sent as an HTTP header for that host only; they are
never part of the artifact ID, the remote URL, the mirror's git config, the
command line or the logs.

### 1. Create a token

Use a dedicated machine user (e.g. `backpack-mirror`) that has **read** access
to the repositories you want to mirror, and create a personal access token for it:

- **Fine-grained token** (preferred): repository access to the repos or org you
  mirror, permissions *Contents: Read-only* (Metadata is added automatically).
- **Classic token** (if your instance doesn't support fine-grained tokens):
  `repo` scope. Its effective access is limited to what the machine user can read.

Give the token an expiry date. If the organization enforces SAML SSO,
authorize the token for it.

### 2. Configure the collector

Create a credentials file keyed by host (use `host:port` if the instance runs
on a non-standard port):

```json
{
  "ghe.example.com": {
    "username": "backpack-mirror",
    "token_file": "/run/secrets/ghe-token"
  }
}
```

| Field | Meaning |
| :--- | :--- |
| `username` | Account that owns the token. Defaults to `x-access-token`. |
| `token_file` | File containing the token (recommended; mount it as a secret). |
| `token_env` | Alternatively, the name of an environment variable holding the token. |
| `token` | The token inline. Only for local testing. |

Point the collector at it with `BP_CREDENTIALS_FILE`, and mount both files
read-only into the **Collector.Git** container only:

```shell
docker run \
  -e BP_CREDENTIALS_FILE=/run/secrets/backpack-credentials.json \
  -v ./secrets/backpack-credentials.json:/run/secrets/backpack-credentials.json:ro \
  -v ./secrets/ghe-token:/run/secrets/ghe-token:ro \
  ... ghcr.io/linus-berg/backpack/collector/git
```

On Kubernetes, put both files in a `Secret` and mount it as a volume.

The token file is read every time a repository is fetched, so rotating the
token is just replacing the file; no restart is needed. The credentials file
itself is read once, when the collector handles its first request; changes to
it need a restart. A missing or malformed file does not stop the collector:
every request fails with that error and is discarded, so check the log after
deploying.

### 3. Add repositories

Add them to the `git` processor like public ones, e.g.
`git://ghe.example.com/my-org/my-repo`. Explicit ports other than 9418 are
kept, e.g. `git://ghe.example.com:8443/my-org/my-repo`.

### Troubleshooting

When a fetch fails, the collector logs git's error output as a warning and
then one of:

- *"Mirroring failed using the credentials configured for &lt;host&gt;"*: the
  token is wrong, expired, not SSO-authorized, or can't read that repository.
- *"Mirroring failed. If the repository is private, add credentials for
  &lt;host&gt;"*: there is no entry for the host in the credentials file.

git never prompts for a password (`GIT_TERMINAL_PROMPT=0`), so a missing or
rejected credential fails immediately instead of hanging.
