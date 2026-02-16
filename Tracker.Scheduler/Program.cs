using System.Text.Json;
using Core.Infrastructure;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Core.Services;
using MassTransit;
using Microsoft.IdentityModel.Protocols.Configuration;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using StackExchange.Redis;
using Tracker.Scheduler;

ModuleRegistration registration = new(ModuleType.CORE, typeof(IHost));

string? file = Environment.GetEnvironmentVariable("SCHEDULE_FILE");
if (file.IsNullOrEmpty()) {
  throw new InvalidConfigurationException("SCHEDULE_FILE is not defined.");
}

if (!File.Exists(file)) {
  throw new FileNotFoundException($"{file} not found.");
}

string schedule_str = await File.ReadAllTextAsync(file);
List<ScheduleOptions>? schedule_opts =
  JsonSerializer.Deserialize<List<ScheduleOptions>>(schedule_str);

if (schedule_opts == null) {
  throw new ArgumentNullException("Schedule could not be parsed");
}

IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices(
                   services => {
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
                           j => j.WithIdentity(TrackingJob.S_KEY)
                         );
                         foreach (ScheduleOptions opts in schedule_opts) {
                           q.AddTrigger(
                             t => {
                               t.WithIdentity(
                                 $"tracking-{opts.processor}",
                                 "backpack"
                               );
                               t.ForJob(TrackingJob.S_KEY);
                               t.UsingJobData("processor", opts.processor);
                               t.WithCronSchedule(opts.schedule);
                             }
                           );
                         }
                       }
                     );

                     services.AddQuartzHostedService(
                       q => { q.WaitForJobsToComplete = true; }
                     );
                   }
                 )
                 .Build();
await host.RunAsync();