// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Exceptions;
using Core.Kernel.Health;
using Core.Kernel.Registrations;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Kernel;

/// <summary>
///   Provides utility methods for registering modules and configuring MassTransit with RabbitMQ.
/// </summary>
public static class RegistrationUtils {
  /// <summary>
  ///   Registers a module with MassTransit and configures its endpoints.
  /// </summary>
  /// <param name="sc">The service collection to register with.</param>
  /// <param name="registration">The module registration information.</param>
  /// <returns>The updated service collection.</returns>
  public static IServiceCollection Register(this IServiceCollection sc,
                                            ModuleRegistration registration) {
    sc.AddScoped<IEventService, EventService>();
    /* /healthz/live and /healthz/ready; MassTransit adds the RabbitMQ
       check, collectors with storage add S3 (Collector.Kernel). */
    sc.AddHealthEndpoint();
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
  ///   Configures retry and redelivery policies for a RabbitMQ receive endpoint.
  ///   <list type="bullet">
  ///     <item>
  ///       <see cref="ArtifactStorageException" />: Backpack's own storage is
  ///       failing, which affects every message. The storage call has already
  ///       waited for it (see Collector.Kernel's FileSystem), so it is not
  ///       retried at once but redelivered every 15 minutes for a day.
  ///     </item>
  ///     <item>
  ///       <see cref="ArtifactRateLimitedException" />: delayed redelivery
  ///       only (5, 15 and 30 minutes).
  ///     </item>
  ///     <item>
  ///       Other <see cref="ArtifactTransientException" />s (timeouts,
  ///       upstream failures): 5 immediate retries, then delayed redelivery
  ///       (5, 15 and 30 minutes).
  ///     </item>
  ///     <item>Anything else, including <see cref="ArtifactMetadataException" />: not retried.</item>
  ///   </list>
  /// </summary>
  /// <param name="endpoint">The endpoint configurator.</param>
  private static void ConfigureRetrying(
    this IRabbitMqReceiveEndpointConfigurator endpoint) {
    /* Outer redelivery: storage outages can last, so keep trying for a day.
       The inner one below ignores storage failures, so they end up here. */
    endpoint.UseDelayedRedelivery(
      r => {
        r.Handle<ArtifactStorageException>();
        r.Interval(96, TimeSpan.FromMinutes(15));
      }
    );
    endpoint.UseDelayedRedelivery(
      r => {
        r.Handle<ArtifactTransientException>();
        r.Ignore<ArtifactMetadataException>();
        r.Ignore<ArtifactStorageException>();
        r.Intervals(
          TimeSpan.FromMinutes(5),
          TimeSpan.FromMinutes(15),
          TimeSpan.FromMinutes(30)
        );
      }
    );
    endpoint.UseMessageRetry(
      r => {
        r.Handle<ArtifactTransientException>();
        r.Ignore<ArtifactMetadataException>();
        /* Retrying at once would only hit the rate limit again. */
        r.Ignore<ArtifactRateLimitedException>();
        /* The storage call already waited for storage to come back. */
        r.Ignore<ArtifactStorageException>();
        r.Immediate(5);
      }
    );

    // Due to not handling *_error queues, discard faulting messages.
    endpoint.DiscardFaultedMessages();
  }

  /// <summary>
  ///   Sets up the RabbitMQ host and authentication for the bus factory.
  /// </summary>
  /// <param name="cfg">The bus factory configurator.</param>
  public static void SetupRabbitMq(this IRabbitMqBusFactoryConfigurator cfg) {
    cfg.Host(
      Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_HOST),
      "/",
      h => {
        h.Username(
          Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER)
        );
        h.Password(
          Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS)
        );
      }
    );
  }
}