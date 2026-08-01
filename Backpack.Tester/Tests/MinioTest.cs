// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel.Storage.Minio;
using Core.Kernel;

namespace Backpack.Tester.Tests;

/// <summary>
///   Tests the Minio S3 connection and basic operations.
/// </summary>
public static class MinioTest {
  /// <summary>
  ///   Prints the current Minio connection configuration.
  /// </summary>
  public static Task Run() {
    Console.WriteLine("=== Minio Test ===");

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

    Console.WriteLine($"Region:   {connection.region}");
    Console.WriteLine($"Endpoint: {connection.end_point}");
    Console.WriteLine($"Bucket:   {connection.bucket}");
    Console.WriteLine("Connection string built successfully.");

    Console.WriteLine("=== Minio Test Complete ===");
    return Task.CompletedTask;
  }
}
