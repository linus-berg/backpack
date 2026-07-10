using Core.Infrastructure;
using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Core.Services;
using Core.Gateway;
using Wolverine;
using Wolverine.RabbitMQ;
using StackExchange.Redis;

ModuleRegistration registration = new(ModuleType.CORE, typeof(IHost));
registration.endpoints = new List<Endpoint> {
  new Endpoint("gateway-ingest-processed"),
  new Endpoint("gateway-ingest-processed-raw"),
  new Endpoint("gateway-ingest-unprocessed"),
  new Endpoint("system-event")
};

IHost host = Host.CreateDefaultBuilder(args)
                 .AddLogging(registration)
                 .UseBackpackWolverine(registration, opts => {
                     opts.ListenToRabbitQueue("gateway-ingest-processed-raw")
                         .DefaultIncomingMessage<Core.Kernel.Messages.ArtifactProcessedRequest>()
                         ;
                 })

                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);

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
                     services
                       .AddScoped<IGatewayProcessingService,
                         GatewayProcessingService>();
                     services.AddScoped<IEventService, EventService>();

                   }
                 )
                 .Build();

await host.RunAsync();