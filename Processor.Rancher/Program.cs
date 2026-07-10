using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Library.Github;
using Processor.Rancher;

ModuleRegistration registration = new(ModuleType.PROCESSOR, typeof(Consumer));
registration.AddEndpoint("rancher");
registration.AddEndpoint("rancher-preview");
IHost host = Host.CreateDefaultBuilder(args)
                 .UseBackpackWolverine(registration)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddSingleton<IGithubClient, GithubClient>();
                     services.AddSingleton<IRancher, Rancher>();
                     
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();