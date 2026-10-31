# Library.Github

A minimal GitHub REST client built on RestSharp. Processor.Github.Releases and
Processor.Rancher use it, and both register it as a singleton
(`services.AddSingleton<IGithubClient, GithubClient>()`). It provides one operation:
listing a repository's releases. It has no caching, no retries and no rate-limit handling.

## API

`IGithubClient.GetReleases(string repo)` returns `List<GithubRelease>`. The `repo` argument
is `<owner>/<repo>`, for example `cli/cli`.

- It requests `GET https://api.github.com/repos/<repo>/releases?page=<n>&per_page=10` for
  `n = 1, 2, ...`, one request per page of 10 releases.
- It drops drafts and prereleases.
- It stops when a page is empty, or when the kept releases reach 100 or more
  (`C_MAX_RELEASES_`, GithubClient.cs:27). A full last page can bring the total to 109.
  GitHub lists the newest releases first, so older releases are never returned.
- A repository with many prereleases needs more pages, and so more requests, to reach 100.

| Model | Fields |
| :--- | :--- |
| `GithubRelease` | `tag_name`, `draft`, `prerelease`, `assets`, `tarball_url`. `GetReleaseFile(name)` returns the `browser_download_url` of the asset with exactly that name, or `""`. |
| `GithubReleaseAsset` | `name`, `browser_download_url` |

## Authentication

The constructor reads `GITHUB_API_KEY` once (GithubClient.cs:14-23).

- **Set and non-empty**: every request carries `Authorization: Bearer <value>` (RestSharp
  `JwtAuthenticator`). Use a GitHub personal access token. Public repositories need no
  scopes.
- **Unset or empty**: requests are anonymous.

GitHub documents limits of 60 requests per hour per IP address for anonymous clients and
5,000 per hour for authenticated users. Set the variable on every container that uses the
library: processor/github-releases and processor/rancher.

## Errors

RestSharp throws on non-success responses and network errors. Examples are 404 for an
unknown repository and 403 or 429 when rate limited. `GetReleasePage` writes the exception to
stdout and rethrows it unchanged (GithubClient.cs:43-53). Because it is neither
`ArtifactTimeoutException` nor `ArtifactMetadataException`, the calling processor's message
faults immediately. It is not retried and is discarded (Core.Kernel/RegistrationUtils.cs).
