// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Git;
using Collector.Kernel;
using Library.Github;
using Library.Skopeo;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Timeout;
using Processor.HuggingFace;
using Processor.Pypi;
using Processor.Terraform;

namespace Backpack.Tester;

/// <summary>
///   Builds the shared service provider used by most tests.
///   Registers storage, processors, collectors, and common services.
/// </summary>
public static class ServiceSetup {
  /// <summary>
  ///   Creates a configured service provider and HTTP client for test use.
  /// </summary>
  /// <returns>A tuple of the service provider and a shared HTTP client.</returns>
  public static (IServiceProvider sp, HttpClient http_client) Build() {
    HttpClient hc = new(
      new HttpClientHandler {
        AllowAutoRedirect = true
      }
    );
    hc.DefaultRequestHeaders.Add("User-Agent", "Backpack/1.0");

    ServiceCollection services = new();
    services.AddStorage();
    services.AddResiliencePipeline<string, bool>(
      "git-timeout",
      builder => {
        builder.AddTimeout(
          new TimeoutStrategyOptions {
            Timeout = TimeSpan.FromMinutes(10)
          }
        );
      }
    );

    services.AddLogging();
    services.AddSingleton<FileSystem>();
    services.AddSingleton<Git>();
    services.AddSingleton<IPypi, Pypi>();
    services.AddSingleton<ITerraform, Terraform>();
    services.AddSingleton<IHuggingFace, HuggingFace>();
    services.AddSingleton<IGithubClient, GithubClient>();
    services.AddSingleton<SkopeoClient>();

    IServiceProvider sp = services.BuildServiceProvider();
    return (sp, hc);
  }
}
