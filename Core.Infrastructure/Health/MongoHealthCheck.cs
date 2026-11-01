// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Infrastructure.Health;

/// <summary>
///   Pings MongoDB (the <c>backpack</c> database at BP_MONGO_STR).
/// </summary>
public sealed class MongoHealthCheck : IHealthCheck {
  /* One client for all checks: MongoClient keeps a connection pool. */
  private static readonly Lazy<MongoClient> S_CLIENT_ = new(
    () => new MongoClient(
      Configuration.GetBackpackVariable(CoreVariables.BP_MONGO_STR)
    )
  );

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken token = default) {
    try {
      await S_CLIENT_.Value
                     .GetDatabase("backpack")
                     .RunCommandAsync<BsonDocument>(
                       new BsonDocument("ping", 1),
                       cancellationToken: token
                     );
      return HealthCheckResult.Healthy("MongoDB answers.");
    } catch (Exception e) {
      return new HealthCheckResult(
        context.Registration.FailureStatus,
        "MongoDB is unreachable.",
        e
      );
    }
  }
}
