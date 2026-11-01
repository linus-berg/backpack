# Processor.Jetbrains.Plugins

Mirrors plugin builds from the JetBrains Marketplace. Image:
`ghcr.io/linus-berg/backpack/processor/jetbrains-plugins`.

## Queues

| Queue | Handles | Concurrency |
| :--- | :--- | :--- |
| `processor-jetbrains-plugins` | `ArtifactProcessRequest`, replies `ArtifactProcessedRequest` to `gateway-ingest-processed` | 10 |
| `processor-jetbrains-plugins-preview` | `ArtifactPreviewRequest`, responds with `ArtifactPreviewResponse` | 10 |

The processor document in `backpack-processors` must have `_id: "jetbrains-plugins"`.
Compose/mongo-init.js creates a processor named `jetbrains` instead. Artifacts of that
processor are sent to `processor-jetbrains`, which nothing consumes.

## Artifact ID

The numeric Marketplace plugin id, optionally followed by `-<slug>` as in the plugin URL.
Both `7724` and `7724-docker` work (from `https://plugins.jetbrains.com/plugin/7724-docker`).
Everything from the first `-` onwards is dropped for the API call, but the full string
remains the artifact id.

## Configuration

| `artifact.config` key | Default | Meaning |
| :--- | :--- | :--- |
| `channels` | stable | Marketplace channels to mirror: a comma-separated list such as `stable, eap`, or `all` for every channel the plugin has. Channel names are the plugin's own (`eap`, `nightly`, `dev`, ...), and `stable` stands for the Marketplace's unnamed default channel. |

To offer the setting in the GUI's add form, give the processor definition a config field:

```json
{ "channels": { "key": "channels", "name": "Channels", "type": "string",
                "placeholder": "stable, eap, nightly or all", "default": "stable" } }
```

Pre-release channels can be large: GitHub Copilot has about 1,350 nightly builds, IdeaVim
about 1,000 on `dev`. Narrow them with a version filter.

## What it reports

For each channel, the processor reads every page of
`GET https://plugins.jetbrains.com/api/plugins/<numeric id>/updates?channel=<channel>&size=100&page=<n>`.
The Marketplace returns at most 100 updates per page, newest first, and names the next page
in the `Link` header. Paging stops when there is no next page, or when a page is empty or
holds nothing new. With `channels: all`, the channel list comes from
`GET /api/plugins/<id>/channels`. No IDE build parameters are sent, so builds for every IDE
version are reported.

- **Versions**: the `version` of each update. An update repeated on the next page, because
  one was published while paging, is skipped. Some plugins (IdeaVim, for example) publish the
  same version on several channels as separate uploads; the first channel's copy is kept,
  and stable always comes first.
- **Files**: one per version, named `plugin`, with URI
  `https://plugins.jetbrains.com/files/<file>`. Here `<file>` is the update's `file` path,
  for example `7724/<updateId>/<name>.zip`. The folder is empty, so the module is
  `jetbrains-plugins`, and the file is stored as
  `jetbrains-plugins/plugins.jetbrains.com/files/7724/<updateId>/<name>.zip`.
- **Dependencies**: plugin dependencies are not followed.
- **collect_requests**: none.

## Preview

The preview handler runs the same lookup on a new artifact, with the request's config, so
`channels` applies. It returns the result, or the exception message in `error`.

## Logging

One line per channel, e.g. `Plugin 7724, channel stable: 852 updates on 9 pages, 852
versions added`.

## Environment

Only the shared variables `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` and
`BP_OTEL_HOST`. No credentials are used.

## Failure behaviour

A failure on any page fails the whole artifact, so a track never stores a partial version
list.

- **Retried** (`ArtifactTimeoutException`: 5 immediate retries, then redelivery after 5, 15
  and 30 minutes): a timeout (60 seconds per request), no connection, HTTP 429 (rate
  limited) and HTTP 5xx.
- **Not retried** (`ArtifactMetadataException`), with the Marketplace's message where it
  gives one: an unknown id (HTTP 404, "Can't find Plugin with id ..."), a non-numeric id
  (HTTP 400, "Parameter 'pluginId' is incorrect"), any other 4xx, and a response that isn't
  the expected JSON.
- A channel the plugin doesn't have produces no versions and no error; the processor logs
  `0 updates` for it.
- More than 100,000 updates on one channel stops paging with an `ArtifactMetadataException`.
  No plugin comes close.
