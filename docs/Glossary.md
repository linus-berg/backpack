# Glossary

Terms used throughout the Backpack code and documentation.

## Components

- **Integration.API**: The REST API behind the GUI and scripts. Authenticates users with
  OIDC tokens or API keys, stores artifacts and settings in MongoDB, and starts work by
  sending messages.
- **Core.Gateway**: The service that drives ingestion. It sends artifacts to processors,
  compares their answers with MongoDB, stores changes, routes files for collection and
  follows dependencies. It also stores system events.
- **Processor**: An ecosystem-specific service (npm, NuGet, PyPI, ...) that turns an
  artifact ID into its versions, the URLs of their files, and its direct dependencies. It
  reads upstream metadata only; it doesn't download artifacts or keep state. Its queue is
  `processor-<id>`.
- **Processor definition**: The MongoDB document (`backpack-processors`) that makes an
  ecosystem known to the API and GUI: its ID, description, extra fields and behaviour
  flags. See [Configuration](Configuration.md#processor-definitions).
- **Collector.Router**: Applies an artifact's version filter and sends one collect request
  per file to the collector for the file's URL scheme.
- **Collector**: A protocol-specific service that downloads files and stores them:
  HTTP(S), Git, container images, Hugging Face, wget mirrors. Its queue is
  `collector-<scheme>`, e.g. `collector-https`.
- **Tracker.Scheduler**: Runs re-track schedules, one cron expression per processor, using
  Quartz.NET.

## Concepts

- **Artifact**: One package, image, repository or other unit in an ecosystem, identified
  by `id` and `processor`, with all its versions. See the [Type
  Reference](TypeReference.md).
- **Root artifact**: An artifact someone added (`root: true`). Artifacts that are only in
  Backpack because something depends on them are non-root. Re-tracking starts from roots.
- **Exhaustive collection**: Backpack's default: every version of an artifact and,
  recursively, every version of everything it depends on. A filter narrows down the
  versions of one artifact; its dependencies are still collected in full.
- **Filter**: A regular expression (default) or semantic-version range on an artifact that
  selects which versions are collected. Applied by the router; the processor always
  reports every version.
- **Ingest context**: A GUID created for every ingest and stored in Redis for two days,
  holding the IDs of the artifacts visited so far, so each dependency is processed once
  per ingest even when the dependency graph has cycles.
- **Re-track**: Processing root artifacts again (one artifact, one processor, or
  everything) to pick up new versions. Schedules do this per processor.
- **Validate**: Routing an artifact's stored versions to the collectors again, without
  asking the processor, to fill in missing files. *Force* re-collects existing files of
  direct-collect artifacts.
- **Direct collect**: A processor flag: the artifact ID is itself a URL (e.g.
  `git://github.com/owner/repo`) that goes straight to a collector, without a processor.
- **External processor**: A processor flag (`is_external`): artifacts are only recorded,
  for ecosystems that are synchronised outside Backpack.
- **Approval**: With `requires_approval`, artifacts added by non-administrators wait in
  `pending-approvals` until an administrator approves or rejects them.
- **Preview**: Asking a processor what an artifact would contain before adding it, through
  the queue `processor-<id>-preview`.
- **Module**: The storage namespace of a collected file: the first path segment of its
  object key (`<module>/<host><path>`). Normally the processor ID; a processor can choose
  another one per file (the file's `folder`).
- **Delta link**: A small object under `delta/<module>/<yyyy_MM_dd>/` recorded for every
  newly collected file, pointing at the file's key. Listing one day's delta folder gives
  everything that was added that day, which is what you need to carry across an air gap
  incrementally. See [Storage Architecture](../STORAGE.md#delta-links).
- **Air gap**: A network with no connection to the internet. Backpack runs outside it,
  collects into storage, and the stored files are transferred in.

## Technology

- **MassTransit**: The .NET messaging library Backpack uses on top of RabbitMQ.
- **Skopeo**: The command-line tool Library.Skopeo wraps to inspect and copy container
  images (Collector.Container, Collector.DockerArchive, Processor.Container).
- **Git bundle**: A single file containing a Git repository's objects and refs.
  Collector.Git writes bundles for transfer; Backpack.GitUnpack applies them to
  repositories on the other side.
