# Processor.Github.Releases

Mirrors selected assets of GitHub releases. Each release that is neither a draft nor a
prerelease becomes a version. Its files are the release assets whose names match one of the
configured regular expressions, plus the source tarball if requested. Image:
`ghcr.io/linus-berg/backpack/processor/github-releases`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-github-releases` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-github-releases-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

## Artifact ID

`<owner>/<repo>`, for example `cli/cli`.

## Configuration

| Key | Required | Meaning |
| :--- | :--- | :--- |
| `files` | yes | `;`-separated .NET regular expressions matched against asset names with `Regex.IsMatch`. Matching is unanchored unless you add `^`/`$`. An asset is included if any expression matches. |
| `include_source` | no | If `true` (case-insensitive), adds the release's source tarball as file `source_tar`. Any other value, or no value, adds nothing. |

The backend fills in no defaults. If `files` is missing, processing fails with
`KeyNotFoundException`. An empty element matches every asset, so a trailing `;`, as in
`^gh_.*_linux_amd64\.tar\.gz$;`, selects all assets. Processor document:

```json
{
  "_id": "github-releases",
  "config": {
    "files": { "key": "files", "type": "string", "name": "Asset name regexes, separated by ;", "placeholder": "^gh_.*_linux_amd64\\.tar\\.gz$" },
    "include_source": { "key": "include_source", "type": "string", "name": "Include source tarball (true/false)", "placeholder": "false" }
  },
  "direct_collect": false
}
```

## What it reports

Releases come from Library.Github: newest first, drafts and prereleases dropped, stopping
after about 100 releases.

- **Versions**: `tag_name`. If a tag is already on the artifact, its stored file list is
  kept, because `AddVersion` doesn't overwrite (GithubReleases.cs:62). Changing `files` later
  therefore only affects new releases. A release with no matching asset is still added, with
  no files.
- **Files**: the name is the last path segment of `browser_download_url`, the URI is
  `browser_download_url`, and the folder is empty, so the module is `github-releases`.
  `https://github.com/cli/cli/releases/download/v2.40.0/gh_2.40.0_linux_amd64.tar.gz` is
  stored as
  `github-releases/github.com/cli/cli/releases/download/v2.40.0/gh_2.40.0_linux_amd64.tar.gz`.
- **Source tarball**: `source_tar` uses `tarball_url`
  (`https://api.github.com/repos/cli/cli/tarball/v2.40.0`). It is stored as
  `github-releases/api.github.com/repos/cli/cli/tarball/v2.40.0`, with no file extension.
- **Dependencies** and **collect_requests**: none.

## Preview

Preview uses the `config` sent with the request, so `files` must be filled in. If it isn't,
the preview returns the error "The given key 'files' was not present in the dictionary."

## Environment

- The shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
  `BP_OTEL_HOST`.
- `GITHUB_API_KEY` (optional): a token for api.github.com, described in
  Library.Github/README.md. Without it, release listing is anonymous and subject to GitHub's
  low anonymous rate limit. Asset downloads by Collector.Http don't use it.

## Failure behaviour

None of these failures is retried, and each faulted message is discarded:

- **GitHub API errors** (unknown repository, rate limiting, network) are rethrown unchanged
  by Library.Github.
- **Invalid regular expressions** throw `ArgumentException`.
- **A missing `files` key** throws `KeyNotFoundException`.
