using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Core.Infrastructure.Services;

public static class ProcessorHost {
  public static IHost Create(string[] args, ModuleRegistration registration,
                             Action<IServiceCollection> configure_services) {
    return Host.CreateDefaultBuilder(args)
               .AddLogging(registration)
               .UseBackpackWolverine(registration)
               .ConfigureServices(
                 services => {
                   services.AddTelemetry(registration);
                   configure_services(services);
                   services.AddHostedService<HeartbeatWorker>();
                 }
               )
               .Build();
  }
}

public class HeartbeatWorker : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stopping_token) {
    while (!stopping_token.IsCancellationRequested) {
      await Task.Delay(10000, stopping_token);
    }
  }
}