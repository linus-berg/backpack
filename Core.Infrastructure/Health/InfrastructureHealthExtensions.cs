// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace Core.Infrastructure.Health;

/// <summary>
///   Extension methods for adding checks for the shared infrastructure.
/// </summary>
public static class InfrastructureHealthExtensions {
  /// <summary>
  ///   Adds a readiness check for MongoDB.
  /// </summary>
  /// <param name="builder">The health checks builder.</param>
  /// <returns>The health checks builder.</returns>
  public static IHealthChecksBuilder AddMongoCheck(
    this IHealthChecksBuilder builder) {
    /* A probe must not open a connection of its own on every call, and
       MongoDatabase builds its client in its constructor at scope lifetime, so
       the check gets a client that is registered as the singleton the driver
       expects it to be. */
    builder.Services.TryAddSingleton<IMongoClient>(
      _ => new MongoClient(
        Configuration.GetBackpackVariable(CoreVariables.BP_MONGO_STR)
      )
    );
    return builder.AddCheck<MongoHealthCheck>(
      "mongodb",
      HealthStatus.Unhealthy,
      new[] {
        HealthEndpoint.C_READY_TAG
      },
      HealthEndpoint.S_DEPENDENCY_TIMEOUT
    );
  }

  /// <summary>
  ///   Adds a readiness check for Redis.
  /// </summary>
  /// <remarks>
  ///   Expects an <see cref="StackExchange.Redis.IConnectionMultiplexer" /> to
  ///   already be registered by the host.
  /// </remarks>
  /// <param name="builder">The health checks builder.</param>
  /// <returns>The health checks builder.</returns>
  public static IHealthChecksBuilder AddRedisCheck(
    this IHealthChecksBuilder builder) {
    return builder.AddCheck<RedisHealthCheck>(
      "redis",
      HealthStatus.Unhealthy,
      new[] {
        HealthEndpoint.C_READY_TAG
      },
      HealthEndpoint.S_DEPENDENCY_TIMEOUT
    );
  }
}
