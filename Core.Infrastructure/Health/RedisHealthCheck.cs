// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Core.Infrastructure.Health;

/// <summary>
///   Pings Redis through the service's shared connection.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck {
  private readonly IConnectionMultiplexer redis_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RedisHealthCheck" /> class.
  /// </summary>
  /// <param name="redis">The service's Redis connection.</param>
  public RedisHealthCheck(IConnectionMultiplexer redis) {
    redis_ = redis;
  }

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken token = default) {
    try {
      TimeSpan latency = await redis_.GetDatabase().PingAsync().WaitAsync(token);
      return HealthCheckResult.Healthy(
        $"Redis answers in {latency.TotalMilliseconds:0} ms."
      );
    } catch (Exception e) {
      return HealthResults.Failed(
        context,
        "Redis is unreachable.",
        e
      );
    }
  }
}
