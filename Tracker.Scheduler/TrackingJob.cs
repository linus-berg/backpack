using Core.Infrastructure.Services;
using Core.Kernel.Models;
using Core.Services;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Tracker.Scheduler;

/// <summary>
///   Runs a processor's schedule as a background re-track job. Only one job
///   runs at a time, so when another one is running (for example a
///   validation started in the GUI) the scheduled run waits, visible as
///   "waiting" on the Jobs page, and gives up after three hours.
/// </summary>
public class TrackingJob : IJob {
  public static readonly JobKey S_KEY = new("track-job", "backpack");

  private static readonly TimeSpan S_POLL_ = TimeSpan.FromSeconds(15);
  private static readonly TimeSpan S_GIVE_UP_AFTER_ = TimeSpan.FromHours(3);

  private readonly ICoreDatabase db_;
  private readonly IJobService jobs_;
  private readonly IHostApplicationLifetime lifetime_;
  private readonly ILogger<TrackingJob> logger_;
  private readonly JobRunner runner_;

  public TrackingJob(ILogger<TrackingJob> logger, IJobService jobs,
                     JobRunner runner, ICoreDatabase db,
                     IHostApplicationLifetime lifetime) {
    jobs_ = jobs;
    runner_ = runner;
    db_ = db;
    lifetime_ = lifetime;
    logger_ = logger;
  }

  public async Task Execute(IJobExecutionContext context) {
    string? processor = context.MergedJobDataMap.GetString("processor");
    if (string.IsNullOrEmpty(processor)) {
      throw new InvalidConfigurationException("Processor not defined");
    }

    logger_.LogInformation("Tracking {Processor}", processor);
    /* Quartz doesn't cancel its token when the host stops, so a run waiting
       for another job would hold up shutdown. */
    using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(
      context.CancellationToken,
      lifetime_.ApplicationStopping
    );
    Job? job = null;
    try {
      job = await jobs_.AddWaiting(JobKinds.TRACK, processor, "Scheduler");
      DateTime give_up = DateTime.UtcNow + S_GIVE_UP_AFTER_;
      while (!await jobs_.TryRun(job)) {
        Job? current = await jobs_.Get(job.id);
        if (current == null || current.state != JobStates.WAITING) {
          logger_.LogInformation(
            "Scheduled re-track of {Processor} was {State} while waiting",
            processor,
            current?.state ?? "removed"
          );
          return;
        }

        if (DateTime.UtcNow > give_up) {
          await jobs_.Finish(
            job,
            JobStates.SKIPPED,
            $"Skipped: another job was still running after {S_GIVE_UP_AFTER_.TotalHours:N0} hours. It runs again at its next scheduled time."
          );
          logger_.LogWarning("Scheduled re-track of {Processor} skipped", processor);
          return;
        }

        await Task.Delay(S_POLL_, stop.Token);
      }

      string state = await runner_.Run(job, stop.Token);
      if (state == JobStates.SUCCEEDED) {
        IEnumerable<Schedule> schedules = await db_.GetSchedules();
        Schedule? schedule =
          schedules.FirstOrDefault(s => s.processor == processor);
        if (schedule != null) {
          schedule.last_run = DateTime.UtcNow;
          await db_.UpdateSchedule(schedule);
        }
      }
    } catch (OperationCanceledException) {
      logger_.LogInformation("Scheduled re-track of {Processor} stopped: shutting down", processor);
      if (job is { state: JobStates.WAITING }) {
        await jobs_.Finish(
          job,
          JobStates.INTERRUPTED,
          "The scheduler stopped before it could run. It runs again at its next scheduled time."
        );
      }
    } catch (Exception e) {
      logger_.LogCritical(e.ToString());
    }
  }
}
