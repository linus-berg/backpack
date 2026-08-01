// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;
using Microsoft.Extensions.DependencyInjection;
using Processor.Pypi;

namespace Backpack.Tester.Tests;

/// <summary>
///   Tests the PyPI processor.
/// </summary>
public static class PypiTest {
  /// <summary>
  ///   Processes a PyPI package artifact.
  /// </summary>
  /// <param name="sp">The service provider.</param>
  public static async Task Run(IServiceProvider sp) {
    Console.WriteLine("=== Pypi Test ===");

    IPypi py = sp.GetRequiredService<IPypi>();

    Artifact py_artifact = new() {
      id = "pandas",
      processor = "pypi",
      filter = string.Empty
    };

    Artifact response = await py.ProcessArtifact(py_artifact);
    Console.WriteLine($"Processed artifact: {response.id}");
    Console.WriteLine($"Versions: {response.versions.Count}");

    Console.WriteLine("=== Pypi Test Complete ===");
  }
}
