// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.Infrastructure.Health;

/// <summary>
///   Readiness checks for the services that use MongoDB and Redis (API,
///   gateway, scheduler).
/// </summary>
public static class HealthChecksBuilderExtensions {
  private static readonly TimeSpan S_TIMEOUT_ = TimeSpan.FromSeconds(3);

  /// <summary>
  ///   Adds the <c>mongodb</c> readiness check.
  /// </summary>
  public static IHealthChecksBuilder AddMongo(this IHealthChecksBuilder builder) {
    return builder.AddCheck<MongoHealthCheck>(
      "mongodb",
      HealthStatus.Unhealthy,
      HealthTags.READY_INFRASTRUCTURE,
      S_TIMEOUT_
    );
  }

  /// <summary>
  ///   Adds the <c>redis</c> readiness check. Needs an
  ///   <c>IConnectionMultiplexer</c> registration.
  /// </summary>
  public static IHealthChecksBuilder AddRedis(this IHealthChecksBuilder builder) {
    return builder.AddCheck<RedisHealthCheck>(
      "redis",
      HealthStatus.Unhealthy,
      HealthTags.READY_INFRASTRUCTURE,
      S_TIMEOUT_
    );
  }
}
