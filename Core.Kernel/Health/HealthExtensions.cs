// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Core.Kernel.Health;

/// <summary>
///   Registration of the health endpoint of worker services.
/// </summary>
public static class HealthExtensions {
  /// <summary>
  ///   Adds health checks and the worker health endpoint
  ///   (<see cref="HealthEndpointService" />). Safe to call more than once.
  ///   Dependency checks are added separately, e.g. AddMongo() and AddRedis()
  ///   from Core.Infrastructure; MassTransit adds its RabbitMQ check itself.
  /// </summary>
  /// <param name="services">The services.</param>
  /// <returns>The health checks builder, to add checks to.</returns>
  public static IHealthChecksBuilder AddHealthEndpoint(
    this IServiceCollection services) {
    services.TryAddEnumerable(
      ServiceDescriptor.Singleton<IHostedService, HealthEndpointService>()
    );
    return services.AddHealthChecks();
  }
}
