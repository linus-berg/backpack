// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;
using Microsoft.Extensions.DependencyInjection;
using Processor.Terraform;

namespace Backpack.Tester.Tests;

/// <summary>
///   Tests the Terraform processor.
/// </summary>
public static class TerraformTest {
  /// <summary>
  ///   Processes a Terraform provider artifact.
  /// </summary>
  /// <param name="sp">The service provider.</param>
  public static async Task Run(IServiceProvider sp) {
    Console.WriteLine("=== Terraform Test ===");

    ITerraform tf = sp.GetRequiredService<ITerraform>();

    // Replace with a provider you want to test:
    // Artifact artifact = new() {
    //   id = "hashicorp/aws",
    //   processor = "terraform",
    //   filter = string.Empty
    // };
    // Artifact response = await tf.ProcessArtifact(artifact);
    // Console.WriteLine($"Processed: {response.id}");

    Console.WriteLine("(No default test configured - uncomment and set a provider above)");
    Console.WriteLine("=== Terraform Test Complete ===");
    await Task.CompletedTask;
  }
}
