# Tracker.Scheduler

Worker service that runs tracking on a cron schedule. Schedules are stored in MongoDB and run
with [Quartz.NET](https://www.quartz-scheduler.net/) 3.8 (in-memory job store). Each schedule
belongs to one processor. When it fires, the processor's root artifacts are tracked: re-ingested
through the Gateway, or collected directly for `direct_collect` processors.

## Startup

1. If the MongoDB collection `backpack-schedules` is empty and `SCHEDULE_FILE` names an
   existing file, every entry of that file is inserted as a schedule with a new GUID id. The
   file is ignored once the collection has at least one schedule.
2. `ScheduleManager.ReloadSchedules` builds the Quartz triggers (see below).
3. The host starts the Quartz scheduler and the MassTransit bus.

## Reloading schedules

`ReloadSchedules` removes every trigger in the Quartz group `backpack`. It then reads all
schedules from `backpack-schedules` and creates one cron trigger per schedule:

- trigger key `backpack.tracking-<processor>`
- job `backpack.track-job` (`TrackingJob`)
- job data `processor = <processor>`
- `WithCronSchedule(schedule.cron)`

A schedule that cannot be scheduled (invalid cron, or a second schedule for the same processor)
is logged as `Failed to schedule <processor>` and skipped.

Reloading happens at startup and whenever a `ReloadSchedulesRequest` arrives. Integration.API
publishes one after it adds, updates or deletes a schedule. The consumer's queue gets the
MassTransit default name `ReloadSchedules`. If several scheduler instances run, they share
that queue, so only one of them reloads.

## When a schedule fires

`TrackingJob` runs the schedule as a background re-track job, the same kind the GUI starts
(see "Background jobs" in `Integration.API/README.md`), so it shows up on the GUI's Jobs page
with `started_by: Scheduler`. Only one job runs at a time: if another job is running, for
example a validation started in the GUI, the scheduled run is added as `waiting` and checks
every 15 seconds whether it can start. After three hours of waiting it ends as `skipped`. If
the scheduler stops while the run is waiting, it ends as `interrupted`; either way the next
firing runs normally. Two schedules that fire together therefore run one after the other.

For every root artifact of the processor:

- `direct_collect` processor: `ArtifactCollectRequest` for the artifact id, sent to
  `collector-<scheme>` (without `force`).
- any other processor: `ArtifactIngestRequest` with the stored artifact, sent to
  `gateway-ingest-unprocessed`.

When the job succeeds, `last_run` of the first schedule with that processor is set to the
current UTC time. Other exceptions are logged as critical and the run ends; the next firing
runs normally. A schedule for a processor that doesn't exist ends as a `failed` job. Non-root
artifacts are not tracked directly; the Gateway visits them when a parent artifact changes.

## Cron format

Expressions are Quartz cron expressions: `seconds minutes hours day-of-month month day-of-week
[year]`. Exactly one of day-of-month and day-of-week must be `?`. Triggers use the time zone of
the scheduler's host or container.

| Expression | Meaning |
| :--- | :--- |
| `0 * * * * ?` | every minute, at second 0 |
| `0 0 3 * * ?` | daily at 03:00 |
| `0 0 2 ? * SUN` | Sundays at 02:00 |

Integration.API checks expressions with Cronos (six fields, seconds first) and computes
`next_run` in UTC. The two parsers differ:

- Cronos accepts `*` in both day fields (e.g. `0 0 * * * *`). Quartz rejects that, so the
  schedule is saved and shows a `next_run`, but it never runs.
- Numeric days of the week mean different days: Quartz uses 1 to 7 for Sunday to Saturday,
  Cronos 0 to 6 (and 7) starting with Sunday. Use names (`MON`, `SUN`).
- The API rejects Quartz's optional seventh (year) field.

## SCHEDULE_FILE

A JSON array of objects with `schedule` (cron) and `processor`, e.g. `schedule.json` in this
directory:

```json
[
  { "schedule": "0 * * * * ?", "processor": "nuget" },
  { "schedule": "0 * * * * ?", "processor": "npm" }
]
```

## Configuration

| Variable | Default | Effect |
| :--- | :--- | :--- |
| `SCHEDULE_FILE` | none | Initial schedules, imported only into an empty `backpack-schedules`. The launch profile sets `schedule.json`. |
| `BP_MONGO_STR` | none | MongoDB. Required at startup. |
| `BP_RABBIT_MQ_HOST`, `BP_RABBIT_MQ_USER`, `BP_RABBIT_MQ_PASS` | `localhost`, `guest`, `guest` | RabbitMQ, vhost `/`. |
| `BP_REDIS_HOST`, `BP_REDIS_USER`, `BP_REDIS_PASS` | `localhost`, `default`, `myverylongpassword` | Connected at startup (startup fails if Redis is unreachable), because `ArtifactService` depends on the cache. Tracking itself writes no Redis keys. |
| `BP_OTEL_HOST` | `http://localhost:4318` | OTLP gRPC export of traces and metrics; `""` disables it. Logs only go to the console. |

`dotnet run --project Tracker.Scheduler` uses `Properties/launchSettings.json`. The image is built
from the repository root with `docker build -f Tracker.Scheduler/Dockerfile .`.

## Gotchas

- Only one schedule per processor takes effect, because the trigger key contains only the
  processor id.
- Triggers live in memory. Quartz state is rebuilt from MongoDB on every start; firings missed
  while the service was down are not run.
- `POST /api/scheduler/trigger/{processor}` in Integration.API starts the same re-track job
  without this service, and does not update `last_run`.
