// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel.Storage.Minio;
using Core.Kernel;
using Microsoft.Extensions.DependencyInjection;
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
    /* SETUP STORAGE */
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

    MinioStorageOptions minio_options = new() {
      auto_create_bucket = true,
      connection_string = connection.ToString()
    };
    services.AddSingleton(minio_options);
    services.AddSingleton<MinioStorage>();
    return services;
  }
}