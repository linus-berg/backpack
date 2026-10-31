# Collector.Rsync

Hands `rsync://` locations to an external `rsync-os` binary. The service is not built or
published by CI (it is missing from `.github/workflows/build-and-publish.yml`), and its
Dockerfile does not install `rsync-os`, so it does not work as shipped.

## Queue

| Queue             | Concurrency |
|-------------------|-------------|
| `collector-rsync` | 10          |

## How a request is handled

The collector runs

```console
rsync-os <location> rsync
```

and waits for it to exit. The second argument is hard-coded (`RSync.cs` calls it the
bucket). The request's `module` and `force` fields are ignored. Collector.Rsync itself
writes nothing to S3, checks nothing for existence and writes no delta links; what ends
up in storage depends entirely on `rsync-os`.

## Configuration

Only the shared RabbitMQ/OpenTelemetry variables in
[docs/Configuration.md](../docs/Configuration.md). Collector.Rsync does not use
Collector.Kernel and does not read the `BP_S3_*` variables. `rsync-os` inherits the
service's environment and nothing else; any storage settings it needs have to be
provided in the form `rsync-os` expects.

## Requirements

`rsync-os` on `PATH`. You have to add it to the image yourself.

## Failure behaviour

A non-zero exit code or a missing binary is logged (a warning with `rsync-os`'s stderr),
but the request still completes normally: nothing is retried and the message is not
faulted. `rsync-os` runs without a time limit.
