// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Infrastructure.Health;

/// <summary>
///   Reports whether MongoDB is reachable.
/// </summary>
public class MongoHealthCheck : IHealthCheck {
  private readonly IMongoClient client_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="MongoHealthCheck" /> class.
  /// </summary>
  /// <param name="client">The Mongo client to probe with.</param>
  public MongoHealthCheck(IMongoClient client) {
    client_ = client;
  }

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context, CancellationToken token = default) {
    try {
      /* ping rather than a read: it is answered by any reachable member and
         does not depend on a collection existing yet. */
      await client_.GetDatabase("backpack")
                   .RunCommandAsync<BsonDocument>(
                     new BsonDocument("ping", 1),
                     cancellationToken: token
                   );
      return HealthCheckResult.Healthy();
    } catch (Exception ex) {
      return HealthCheckResult.Unhealthy("MongoDB is not reachable", ex);
    }
  }
}
