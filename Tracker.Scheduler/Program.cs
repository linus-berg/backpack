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
                     services.AddSingleton<ScheduleManager>();
                     services.AddMassTransit(
                       mt => {
                         mt.AddConsumer<ReloadSchedulesConsumer>();
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
                         new ConfigurationOptions {
                           User = Configuration.GetBackpackVariable(
                             CoreVariables.BP_REDIS_USER
                           ),
                           Password =
                             Configuration.GetBackpackVariable(
                               CoreVariables.BP_REDIS_PASS
                             ),
                           EndPoints = new EndPointCollection {
                             Configuration.GetBackpackVariable(
                               CoreVariables.BP_REDIS_HOST
                             )
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
  ScheduleManager manager = scope.ServiceProvider.GetRequiredService<ScheduleManager>();
  await manager.InitializeSchedules();
}

await host.RunAsync();
