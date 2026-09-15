// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.Kernel.Extensions;

/// <summary>
///   Extension methods for wiring up health checks and the health endpoint.
/// </summary>
public static class HealthExtensions {
  /// <summary>
  ///   Registers the baseline health checks every service shares.
  /// </summary>
  /// <remarks>
  ///   Safe to call more than once. Several extension points contribute checks
  ///   to the same host (module registration, storage, the host itself), and
  ///   registering the same check name twice makes
  ///   <see cref="HealthCheckService" /> throw when the first probe arrives
  ///   rather than at startup, which is a long way from the cause.
  /// </remarks>
  /// <param name="services">The service collection.</param>
  /// <returns>The health checks builder, for adding dependency checks.</returns>
  public static IHealthChecksBuilder AddBackpackHealthChecks(
    this IServiceCollection services) {
    IHealthChecksBuilder builder = services.AddHealthChecks();
    if (services.Any(d => d.ServiceType == typeof(SelfCheckMarker))) {
      return builder;
    }

    services.AddSingleton<SelfCheckMarker>();
    return builder.AddCheck(
      "self",
      () => HealthCheckResult.Healthy("The process is running"),
      new[] {
        HealthEndpoint.C_LIVE_TAG
      }
    );
  }

  /// <summary>
  ///   Registers the baseline health checks and exposes them over HTTP on
  ///   <see cref="CoreVariables.BP_HEALTH_PORT" />, at <c>/health/live</c>,
  ///   <c>/health/ready</c> and <c>/health</c>.
  /// </summary>
  /// <remarks>
  ///   For the console hosts only. Services built on the web SDK already have a
  ///   server and should map the endpoints on it instead, so that the probes go
  ///   through the same pipeline as the rest of their routes.
  /// </remarks>
  /// <param name="services">The service collection.</param>
  /// <returns>The updated service collection.</returns>
  public static IServiceCollection AddBackpackHealthEndpoint(
    this IServiceCollection services) {
    services.AddBackpackHealthChecks();
    if (services.Any(d => d.ImplementationType == typeof(HealthEndpoint))) {
      return services;
    }

    services.AddHostedService<HealthEndpoint>();
    return services;
  }

  /// <summary>
  ///   Marker used to keep <see cref="AddBackpackHealthChecks" /> idempotent.
  /// </summary>
  private sealed class SelfCheckMarker {
  }
}
