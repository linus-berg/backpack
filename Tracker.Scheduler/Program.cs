using System.Text.Json;
using Core.Infrastructure;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Models;
using Core.Kernel.Registrations;
using Core.Services;
using MassTransit;
using Quartz;
using StackExchange.Redis;
using Tracker.Scheduler;

ModuleRegistration registration = new(ModuleType.CORE, typeof(IHost));

IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices(
                   (hostContext, services) => {
                     services.AddTelemetry(registration);
                     services.AddHostedService<Worker>();
                     services.AddMassTransit(
                       mt => {
                         mt.UsingRabbitMq(
                           (ctx, cfg) => {
                             cfg.SetupRabbitMq();
                             cfg.ConfigureEndpoints(ctx);
                           }
                         );
                       }
                     );
                     services.AddSingleton<IConnectionMultiplexer>(
                       ConnectionMultiplexer.Connect(
                         new ConfigurationOptions() {
                           User = Configuration.GetBackpackVariable(
                             CoreVariables.BP_REDIS_USER
                           ),
                           Password =
                             Configuration.GetBackpackVariable(
                               CoreVariables.BP_REDIS_PASS
                             ),
                           EndPoints = new EndPointCollection() {
                             Configuration.GetBackpackVariable(
                               CoreVariables.BP_REDIS_HOST
                             ),
                           }
                         }
                       )
                     );
                     services.AddScoped<ICoreDatabase, MongoDatabase>();
                     services.AddSingleton<ICoreCache, CoreCache>();
                     services.AddScoped<IArtifactService, ArtifactService>();

                     services.AddQuartz(
                       q => {
                         q.AddJob<TrackingJob>(
                           j => j.WithIdentity(TrackingJob.S_KEY).StoreDurably()
                         );
                       }
                     );

                     services.AddQuartzHostedService(
                       q => { q.WaitForJobsToComplete = true; }
                     );
                   }
                 )
                 .Build();

// Migrate schedules from file to DB if needed and schedule triggers
using (IServiceScope scope = host.Services.CreateScope()) {
  ICoreDatabase db = scope.ServiceProvider.GetRequiredService<ICoreDatabase>();
  IScheduler scheduler = await scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();

  IEnumerable<Schedule> existing_schedules = await db.GetSchedules();
  if (!existing_schedules.Any()) {
    string? file = Environment.GetEnvironmentVariable("SCHEDULE_FILE");
    if (!string.IsNullOrEmpty(file) && File.Exists(file)) {
      string schedule_str = await File.ReadAllTextAsync(file);
      List<ScheduleOptions>? schedule_opts = JsonSerializer.Deserialize<List<ScheduleOptions>>(schedule_str);
      if (schedule_opts != null) {
        foreach (ScheduleOptions opt in schedule_opts) {
          await db.AddSchedule(new Schedule {
            id = Guid.NewGuid().ToString(),
            processor = opt.processor,
            cron = opt.schedule
          });
        }
      }
    }
  }

  // Reload schedules from DB and add to Quartz
  IEnumerable<Schedule> schedules = await db.GetSchedules();
  foreach (Schedule schedule in schedules) {
    ITrigger trigger = TriggerBuilder.Create()
                                     .WithIdentity($"tracking-{schedule.processor}", "backpack")
                                     .ForJob(TrackingJob.S_KEY)
                                     .UsingJobData("processor", schedule.processor)
                                     .WithCronSchedule(schedule.cron)
                                     .Build();
    await scheduler.ScheduleJob(trigger);
  }
}

await host.RunAsync();