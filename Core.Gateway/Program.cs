using Core.Gateway;
using Core.Gateway.Consumers;
using Core.Gateway.Definitions;
using Core.Infrastructure;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Core.Services;
using MassTransit;
using StackExchange.Redis;

ModuleRegistration registration = new(ModuleType.CORE, typeof(IHost));
IHost host = Host.CreateDefaultBuilder(args)
                 .AddLogging(registration)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddMassTransit(
                       b => {
                         b.AddConsumer<ProcessedConsumer>(
                           typeof(ProcessedDefinition)
                         );
                         b.AddConsumer<ProcessedConsumer>(
                           typeof(ProcessedRawDefinition)
                         );
                         b.AddConsumer<IngestConsumer>(
                           typeof(IngestDefinition)
                         );
                         b.AddConsumer<SystemEventConsumer>(
                           typeof(SystemEventDefinition)
                         );

                         b.UsingRabbitMq(
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
                     services.AddScoped<IEventService, EventService>();
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();