using System.Text.Json;
using Core.Kernel.Models;
using Core.Services;
using Quartz;
using Quartz.Impl.Matchers;

namespace Tracker.Scheduler;

public class ScheduleManager {
  private readonly ILogger<ScheduleManager> logger_;
  private readonly IServiceProvider service_provider_;

  public ScheduleManager(IServiceProvider service_provider,
                         ILogger<ScheduleManager> logger) {
    service_provider_ = service_provider;
    logger_ = logger;
  }

  public async Task InitializeSchedules() {
    using IServiceScope scope = service_provider_.CreateScope();
    ICoreDatabase db =
      scope.ServiceProvider.GetRequiredService<ICoreDatabase>();

    /* Load schedules from disk if no schedules present in database */
    IEnumerable<Schedule> existing_schedules = await db.GetSchedules();
    if (!existing_schedules.Any()) {
      string? file = Environment.GetEnvironmentVariable("SCHEDULE_FILE");
      if (!string.IsNullOrEmpty(file) && File.Exists(file)) {
        string schedule_str = await File.ReadAllTextAsync(file);
        List<ScheduleOptions>? schedule_opts =
          JsonSerializer.Deserialize<List<ScheduleOptions>>(schedule_str);
        if (schedule_opts != null) {
          foreach (ScheduleOptions opt in schedule_opts) {
            await db.AddSchedule(
              new Schedule {
                id = Guid.NewGuid().ToString(),
                processor = opt.processor,
                cron = opt.schedule
              }
            );
          }
        }
      }
    }

    await ReloadSchedules();
  }

  public async Task ReloadSchedules() {
    logger_.LogInformation("Reloading schedules...");
    using IServiceScope scope = service_provider_.CreateScope();
    ICoreDatabase db =
      scope.ServiceProvider.GetRequiredService<ICoreDatabase>();
    IScheduler scheduler = await scope.ServiceProvider
                                      .GetRequiredService<ISchedulerFactory>()
                                      .GetScheduler();

    // Clear existing triggers for backpack group
    IReadOnlyCollection<TriggerKey> trigger_keys =
      await scheduler.GetTriggerKeys(
        GroupMatcher<TriggerKey>.GroupEquals("backpack")
      );
    await scheduler.UnscheduleJobs(trigger_keys.ToList());

    // Load from DB
    IEnumerable<Schedule> schedules = await db.GetSchedules();
    foreach (Schedule schedule in schedules) {
      try {
        ITrigger trigger = TriggerBuilder.Create()
                                         .WithIdentity(
                                           $"tracking-{schedule.processor}",
                                           "backpack"
                                         )
                                         .ForJob(TrackingJob.S_KEY)
                                         .UsingJobData(
                                           "processor",
                                           schedule.processor
                                         )
                                         .WithCronSchedule(schedule.cron)
                                         .Build();
        await scheduler.ScheduleJob(trigger);
        logger_.LogInformation(
          "Scheduled {Processor} with {Cron}",
          schedule.processor,
          schedule.cron
        );
      } catch (Exception ex) {
        logger_.LogError(
          ex,
          "Failed to schedule {Processor}",
          schedule.processor
        );
      }
    }
  }
}