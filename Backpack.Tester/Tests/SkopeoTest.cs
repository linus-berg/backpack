// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Library.Skopeo;

namespace Backpack.Tester.Tests;

/// <summary>
///   Tests the Skopeo container operations (list tags, copy to tar).
/// </summary>
public static class SkopeoTest {
  /// <summary>
  ///   Lists tags for a container image and optionally copies it to a tar archive.
  /// </summary>
  /// <param name="sp">The service provider.</param>
  public static async Task Run(IServiceProvider sp) {
    Console.WriteLine("=== Skopeo Test ===");

    SkopeoClient sk = sp.GetRequiredService<SkopeoClient>();

    SkopeoListTagsOutput? tags = await sk.GetTags("docker.io/amazon/aws-cli");

    if (tags != null) {
      foreach (string tag in tags.tags) {
        Console.WriteLine(tag);
      }
    }

    // Uncomment to test copying to tar archive:
    // await sk.CopyToTar("docker-archive://docker.io/amazon/aws-cli:2.31.12", "docker-archive");

    Console.WriteLine("=== Skopeo Test Complete ===");
  }
}
