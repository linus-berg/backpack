# About

The git ACM processor produces git bundle files to incrementally update repositories.

# How to use git bundles

The first git bundle will be named
`<repository>@00010101000000-<xxx (utc.now)>.bundle`
and the next increment of the bundle will be named as
`<repository>@<xxx>-<yyy (utc.now)>`

The approach I use is as following

```console
# Rename the initial bundle to the repository name
mv <repository>@00010101000000-<xxx>.bundle <repository>

# This creates a mirror for serving via http
git clone --mirror <repository>

# Move the initial bundle to a applied folder and rename to original name
mv <repository> /storage/applied/<repository>@00010101000000-<xxx>.bundle
```

```console
# If serving over http
git update-server-info
```

## To apply the next incremental updates updates do the following:

```console
# Before applying the next update, always verify if it can be applied first!
cd <repository>

# If it is missing previous refs it will exit with non 0 code.
git bundle verify ../<repository>@<xxx>-<yyy>.bundle

cd ..

# Rename the incremental update bundle to.
# NOTE: It is important this bundle is located at the same place as the intial!
mv <repository@<xxx>-<yyy>.bundle <repository>

cd <repository>

git remote update

# If serving over http
git update-server-info

cd ..
# Move the incremental bundle to the applied folder and rename to the original name
mv <repository> /storage/applied/<repository>@<xxx>-<yyy>.bundle
```

# Private repositories (e.g. GitHub Enterprise)

The collector can mirror repositories that need authentication. Credentials
are looked up by host and sent as an HTTP header for that host only; they are
never part of the artifact ID, the remote URL, the mirror's git config, the
command line or the logs.

## 1. Create a token

Use a dedicated machine user (e.g. `backpack-mirror`) that has **read** access
to the repositories you want to mirror, and create a personal access token for it:

- **Fine-grained token** (preferred): repository access to the repos or org you
  mirror, permissions *Contents: Read-only* (Metadata is added automatically).
- **Classic token** (if your instance doesn't support fine-grained tokens):
  `repo` scope. Its effective access is limited to what the machine user can read.

Give the token an expiry date. If the organization enforces SAML SSO,
authorize the token for it.

## 2. Configure the collector

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
itself is read at startup: a missing or malformed file stops the collector
with an error rather than failing later.

## 3. Add repositories

Add them to the `git` processor like public ones, e.g.
`git://ghe.example.com/my-org/my-repo`. Explicit ports other than 9418 are
kept, e.g. `git://ghe.example.com:8443/my-org/my-repo`.

## Troubleshooting

When a fetch fails, the collector logs git's error output as a warning and
then one of:

- *"Mirroring failed using the credentials configured for &lt;host&gt;"*: the
  token is wrong, expired, not SSO-authorized, or can't read that repository.
- *"Mirroring failed. If the repository is private, add credentials for
  &lt;host&gt;"*: there is no entry for the host in the credentials file.

git never prompts for a password (`GIT_TERMINAL_PROMPT=0`), so a missing or
rejected credential fails immediately instead of hanging.
