// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel.Storage.Minio;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Collector.Kernel;

/// <summary>
///   Checks that the S3 storage answers and accepts the credentials. A
///   missing bucket is only degraded: it is created on first write.
/// </summary>
public sealed class S3HealthCheck : IHealthCheck {
  private readonly MinioStorage storage_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="S3HealthCheck" /> class.
  /// </summary>
  /// <param name="storage">The collector's storage.</param>
  public S3HealthCheck(MinioStorage storage) {
    storage_ = storage;
  }

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken token = default) {
    try {
      return await storage_.BucketExistsAsync(token)
               ? HealthCheckResult.Healthy($"Bucket {storage_.bucket} is reachable.")
               : HealthCheckResult.Degraded(
                 $"Bucket {storage_.bucket} doesn’t exist yet; it is created on first write."
               );
    } catch (Exception e) {
      return new HealthCheckResult(
        context.Registration.FailureStatus,
        "S3 storage is unreachable or rejects the credentials.",
        e
      );
    }
  }
}
