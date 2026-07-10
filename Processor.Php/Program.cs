using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Processor.Php;

ModuleRegistration registration = new(ModuleType.PROCESSOR, typeof(Consumer));
registration.AddEndpoint("php", 4);
registration.AddEndpoint("php-preview");

IHost host = Host.CreateDefaultBuilder(args)
                 .UseBackpackWolverine(registration)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddSingleton<IPhp, Php>();
                     
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();