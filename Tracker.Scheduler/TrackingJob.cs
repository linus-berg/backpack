using Core.Kernel.Models;
using Core.Services;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Tracker.Scheduler;

public class TrackingJob : IJob {
  public static readonly JobKey S_KEY = new("track-job", "backpack");
  private readonly IArtifactService aps_;
  private readonly ICoreDatabase db_;
  private readonly ILogger<TrackingJob> logger_;

  public TrackingJob(ILogger<TrackingJob> logger, IArtifactService aps,
                     ICoreDatabase db) {
    aps_ = aps;
    db_ = db;
    logger_ = logger;
  }

  public async Task Execute(IJobExecutionContext context) {
    string? processor = context.MergedJobDataMap.GetString("processor");
    if (string.IsNullOrEmpty(processor)) {
      throw new InvalidConfigurationException("Processor not defined");
    }

    logger_.LogInformation("Tracking {Processor}", processor);
    try {
      await aps_.Track(processor);

      // Update last_run in DB
      IEnumerable<Schedule> schedules = await db_.GetSchedules();
      Schedule? schedule =
        schedules.FirstOrDefault(s => s.processor == processor);
      if (schedule != null) {
        schedule.last_run = DateTime.UtcNow;
        await db_.UpdateSchedule(schedule);
      }
    } catch (Exception e) {
      logger_.LogCritical(e.ToString());
    }
  }
}