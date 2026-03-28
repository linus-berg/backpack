// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Exceptions;
using Core.Kernel.Registrations;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Kernel;

/// <summary>
/// Provides utility methods for registering modules and configuring MassTransit with RabbitMQ.
/// </summary>
public static class RegistrationUtils {
  /// <summary>
  /// Registers a module with MassTransit and configures its endpoints.
  /// </summary>
  /// <param name="sc">The service collection to register with.</param>
  /// <param name="registration">The module registration information.</param>
  /// <returns>The updated service collection.</returns>
  public static IServiceCollection Register(this IServiceCollection sc,
                                            ModuleRegistration registration) {
    sc.AddScoped<IEventService, EventService>();
    sc.AddMassTransit(
      mt => {
        mt.AddConsumer(registration.consumer);
        mt.UsingRabbitMq(
          (ctx, cfg) => {
            /* Absurdly high timeout */
            cfg.UseTimeout(t => t.Timeout = TimeSpan.FromMinutes(180));
            foreach (Endpoint endpoint in registration.endpoints) {
              cfg.ReceiveEndpoint(
                endpoint.name,
                c => {
                  c.ConfigureRetrying();
                  c.ConcurrentMessageLimit = endpoint.concurrency;

                  // use the outbox to prevent duplicate events from being published
                  c.UseInMemoryOutbox(ctx);
                  /* Absurdly high timeout */
                  c.UseTimeout(x => x.Timeout = TimeSpan.FromMinutes(180));
                  c.ConfigureConsumer(ctx, registration.consumer);
                }
              );
            }

            cfg.SetupRabbitMq();
            cfg.ConfigureEndpoints(ctx);
          }
        );
      }
    );
    return sc;
  }

  /// <summary>
  /// Configures retry and redelivery policies for a RabbitMQ receive endpoint.
  /// </summary>
  /// <param name="endpoint">The endpoint configurator.</param>
  private static void ConfigureRetrying(
    this IRabbitMqReceiveEndpointConfigurator endpoint) {
    endpoint.UseDelayedRedelivery(
      r => {
        r.Handle<ArtifactTimeoutException>();
        r.Ignore<ArtifactMetadataException>();
        r.Intervals(
          TimeSpan.FromMinutes(5),
          TimeSpan.FromMinutes(15),
          TimeSpan.FromMinutes(30)
        );
      }
    );
    endpoint.UseMessageRetry(
      r => {
        r.Handle<ArtifactTimeoutException>();
        r.Ignore<ArtifactMetadataException>();
        r.Immediate(5);
      }
    );

    // Due to not handling *_error queues, discard faulting messages.
    endpoint.DiscardFaultedMessages();
  }

  /// <summary>
  /// Sets up the RabbitMQ host and authentication for the bus factory.
  /// </summary>
  /// <param name="cfg">The bus factory configurator.</param>
  public static void SetupRabbitMq(this IRabbitMqBusFactoryConfigurator cfg) {
    cfg.Host(
      Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_HOST),
      "/",
      h => {
        h.Username(Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER));
        h.Password(Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS));
      }
    );
  }
}
