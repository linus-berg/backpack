// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel.Storage.Minio;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Collector.Kernel.Health;

/// <summary>
///   Reports whether the object storage backend and its bucket are reachable.
/// </summary>
public class StorageHealthCheck : IHealthCheck {
  private readonly MinioStorage storage_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="StorageHealthCheck" /> class.
  /// </summary>
  /// <param name="storage">The storage backend to probe.</param>
  public StorageHealthCheck(MinioStorage storage) {
    storage_ = storage;
  }

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context, CancellationToken token = default) {
    try {
      if (!await storage_.BucketExistsAsync(token)) {
        return HealthCheckResult.Unhealthy(
          "The configured bucket does not exist"
        );
      }

      return HealthCheckResult.Healthy();
    } catch (Exception ex) {
      return HealthCheckResult.Unhealthy("Object storage is not reachable", ex);
    }
  }
}
