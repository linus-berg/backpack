using Core.Infrastructure.Services;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Registrations;
using Processor._NAME_;

ModuleRegistration registration = new(ModuleType.PROCESSOR, typeof(Consumer));
registration.AddEndpoint("_ENDPOINT_");

IHost host = ProcessorHost.Create(args, registration, services => {
    services.AddSingleton<I_NAME_, _NAME_>();
});

await host.RunAsync();
