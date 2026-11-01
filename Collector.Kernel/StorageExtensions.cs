// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel.Storage.Minio;
using Core.Kernel;
using Core.Kernel.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.Retry;

namespace Collector.Kernel;

/// <summary>
///   Extension methods for adding storage services.
/// </summary>
public static class StorageExtensions {
  /// <summary>
  ///   Adds storage services to the service collection.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>The updated service collection.</returns>
  public static IServiceCollection AddStorage(
    this IServiceCollection services) {
    services.AddResiliencePipeline<string, bool>(
      "storage-pipeline",
      builder => {
        builder.AddRetry(
          new RetryStrategyOptions<bool> {
            Delay = TimeSpan.FromSeconds(5),
            MaxRetryAttempts = 5
          }
        );
      }
    );
    return services.AddS3Storage(HealthStatus.Unhealthy, true);
  }

  /// <summary>
  ///   Registers <see cref="MinioStorage" /> for the BP_S3_* settings and the
  ///   <c>s3</c> health check. Reads the settings now, so a missing variable
  ///   fails while the host is built.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="failure_status">
  ///   What an unreachable S3 means for readiness: unhealthy for collectors,
  ///   degraded for services that only report it.
  /// </param>
  /// <param name="auto_create_bucket">Create the bucket on first write.</param>
  /// <returns>The updated service collection.</returns>
  public static IServiceCollection AddS3Storage(
    this IServiceCollection services,
    HealthStatus failure_status,
    bool auto_create_bucket) {
    MinioConnectionBuilder connection = new();

    connection.region =
      Configuration.GetBackpackVariable(CoreVariables.BP_S3_REGION);
    connection.access_key =
      Configuration.GetBackpackVariable(CoreVariables.BP_S3_ACCESS_KEY);
    connection.secret_key =
      Configuration.GetBackpackVariable(CoreVariables.BP_S3_SECRET_KEY);
    connection.end_point =
      Configuration.GetBackpackVariable(CoreVariables.BP_S3_ENDPOINT);
    connection.bucket =
      Configuration.GetBackpackVariable(CoreVariables.BP_S3_BUCKET);

    bool tracing = bool.Parse(
      Configuration.GetBackpackVariable(CoreVariables.BP_S3_TRACING)
    );
    MinioStorageOptions minio_options = new() {
      auto_create_bucket = auto_create_bucket,
      connection_string = connection.ToString(),
      tracing = tracing
    };
    services.AddSingleton(minio_options);
    services.AddSingleton<MinioStorage>();
    services.AddHealthChecks()
            .AddCheck<S3HealthCheck>(
              "s3",
              failure_status,
              HealthTags.READY_INFRASTRUCTURE,
              TimeSpan.FromSeconds(3)
            );
    return services;
  }
}