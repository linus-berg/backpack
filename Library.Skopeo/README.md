# Library.Skopeo

Library.Skopeo is a wrapper around the Skopeo CLI tool, used within the Backpack ecosystem to inspect and manage
container images.

## Key Components

- **SkopeoService**: Provides an interface for executing Skopeo commands such as:
    - `skopeo inspect`: To get image metadata and tags.
    - `skopeo list-tags`: To list available tags for a repository.
- **Process Execution**: Handles the execution of the Skopeo binary and parses its JSON output.

## Interaction with Other Services

This library is primarily used by `Processor.Container` to extract metadata and tags for container images from
registries. It may also be used by `Collector.Container` for certain image manipulation tasks.
