using Core.Infrastructure;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Core.Services;
using Wolverine;
using Wolverine.RabbitMQ;
using Quartz;
using StackExchange.Redis;
using Tracker.Scheduler;

ModuleRegistration registration = new(ModuleType.CORE, typeof(IHost));
// We can just add the endpoint to the registration so Wolverine sets up the queue
registration.endpoints = new List<Endpoint> {
  new Endpoint("scheduler")
};

IHost host = Host.CreateDefaultBuilder(args)
                 .UseBackpackWolverine(registration)
                 .ConfigureServices(
                   (hostContext, services) => {
                     services.AddTelemetry(registration);
                     services.AddHostedService<Worker>();
                     services.AddSingleton<ScheduleManager>();
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
  ScheduleManager manager =
    scope.ServiceProvider.GetRequiredService<ScheduleManager>();
  await manager.InitializeSchedules();
}

await host.RunAsync();