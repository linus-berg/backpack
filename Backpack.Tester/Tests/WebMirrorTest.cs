// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Collector.Wget;
using Core.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backpack.Tester.Tests;

/// <summary>
///   Tests the native WebMirror engine by mirroring a URL to S3.
/// </summary>
public static class WebMirrorTest {
  /// <summary>
  ///   Mirrors a website to S3 storage.
  /// </summary>
  /// <param name="url">The URL to mirror.</param>
  public static async Task Run(string url) {
    Console.WriteLine("=== WebMirror Test ===");
    Console.WriteLine($"Target URL: {url}");
    Console.WriteLine();

    ServiceCollection services = new();
    services.AddStorage();
    services.AddLogging(
      builder => {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);
      }
    );
    services.AddSingleton<FileSystem>();
    services.AddHttpClient("mirror-client")
            .ConfigureHttpClient(
              client => {
                client.DefaultRequestHeaders.UserAgent
                      .ParseAdd("Backpack/1.0");
                client.Timeout = TimeSpan.FromMinutes(5);
              }
            )
            .ConfigurePrimaryHttpMessageHandler(
              () => new HttpClientHandler {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression =
                  System.Net.DecompressionMethods.All
              }
            );
    services.AddSingleton<WebMirror>();

    IServiceProvider sp = services.BuildServiceProvider();
    WebMirror web_mirror = sp.GetRequiredService<WebMirror>();

    Console.WriteLine("Starting mirror...");
    bool success = await web_mirror.Mirror(url);
    Console.WriteLine();
    Console.WriteLine(success
      ? "Mirror completed successfully."
      : "Mirror failed. Check logs above for details.");
    Console.WriteLine("=== WebMirror Test Complete ===");
  }
}
