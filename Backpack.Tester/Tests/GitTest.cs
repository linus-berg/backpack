// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Git;
using Microsoft.Extensions.DependencyInjection;

namespace Backpack.Tester.Tests;

/// <summary>
///   Tests the Git collector mirror operation.
/// </summary>
public static class GitTest {
  /// <summary>
  ///   Mirrors a Git repository.
  /// </summary>
  /// <param name="sp">The service provider.</param>
  public static async Task Run(IServiceProvider sp) {
    Console.WriteLine("=== Git Test ===");

    Git git = sp.GetRequiredService<Git>();
    await git.Mirror("git://github.com/linus-berg/ATM.Npm", CancellationToken.None);

    Console.WriteLine("=== Git Test Complete ===");
  }
}
