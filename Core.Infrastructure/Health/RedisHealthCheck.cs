// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Core.Infrastructure.Health;

/// <summary>
///   Reports whether Redis is reachable.
/// </summary>
public class RedisHealthCheck : IHealthCheck {
  private readonly IConnectionMultiplexer redis_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RedisHealthCheck" /> class.
  /// </summary>
  /// <param name="redis">The connection multiplexer to probe with.</param>
  public RedisHealthCheck(IConnectionMultiplexer redis) {
    redis_ = redis;
  }

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context, CancellationToken token = default) {
    try {
      /* IsConnected is checked first: the multiplexer queues commands while it
         reconnects, so a ping alone can sit waiting instead of reporting. */
      if (!redis_.IsConnected) {
        return HealthCheckResult.Unhealthy("Redis is not connected");
      }

      TimeSpan latency = await redis_.GetDatabase().PingAsync();
      return HealthCheckResult.Healthy($"Ping took {latency.TotalMilliseconds:F0}ms");
    } catch (Exception ex) {
      return HealthCheckResult.Unhealthy("Redis is not reachable", ex);
    }
  }
}
