// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Exceptions;
using Core.Kernel.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.ErrorHandling;

namespace Core.Kernel;

public static class RegistrationUtils {
  public static IHostBuilder UseBackpackWolverine(this IHostBuilder builder, ModuleRegistration registration, Action<WolverineOptions> configureWolverine = null) {
    return builder.UseWolverine(
      opts => {
        opts.UseRabbitMq(
          h => {
            h.HostName = Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_HOST);
            h.UserName = Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER);
            h.Password = Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS);
          }
        ).AutoProvision();

        foreach (Endpoint endpoint in registration.endpoints) {
          opts.ListenToRabbitQueue(endpoint.name)
              .MaximumParallelMessages(endpoint.concurrency);
        }

        opts.Policies.OnException<ArtifactTimeoutException>()
            .RetryWithCooldown(
              TimeSpan.FromMinutes(5),
              TimeSpan.FromMinutes(15),
              TimeSpan.FromMinutes(30)
            );
            
        opts.Policies.OnException<ArtifactMetadataException>()
            .Discard();

        opts.Services.AddScoped<IEventService, EventService>();
        
        configureWolverine?.Invoke(opts);
      }
    );
  }

  public static IServiceCollection Register(this IServiceCollection sc, ModuleRegistration registration) {
    // Keep this for backwards compatibility if needed, or remove it and update everything
    sc.AddScoped<IEventService, EventService>();
    return sc;
  }
}
