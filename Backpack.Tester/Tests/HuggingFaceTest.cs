// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Core.Kernel.Models;
using Microsoft.Extensions.DependencyInjection;
using Processor.HuggingFace;
using RemoteFile = Collector.Huggingface.RemoteFile;

namespace Backpack.Tester.Tests;

/// <summary>
///   Tests the HuggingFace processor and collector.
/// </summary>
public static class HuggingFaceTest {
  /// <summary>
  ///   Processes a HuggingFace model artifact and downloads all files.
  /// </summary>
  /// <param name="sp">The service provider.</param>
  /// <param name="http_client">The HTTP client for downloads.</param>
  public static async Task Run(IServiceProvider sp, HttpClient http_client) {
    Console.WriteLine("=== HuggingFace Test ===");

    IHuggingFace hf = sp.GetRequiredService<IHuggingFace>();
    FileSystem fs = sp.GetRequiredService<FileSystem>();

    Artifact artifact = await hf.ProcessArtifact(
                          new Artifact() {
                            id = "google/gemma-4-31B-it-assistant",
                            processor = "huggingface",
                            filter = string.Empty
                          }
                        );

    foreach (KeyValuePair<string, ArtifactVersion> version in artifact.versions) {
      foreach (KeyValuePair<string, ArtifactFile> file in version.Value.files) {
        string location = file.Value.uri;
        string fp = fs.GetArtifactPath("huggingface", location);
        RemoteFile rf = new RemoteFile(http_client, file.Value.uri, fs);
        await rf.Get(fp);
      }

      Console.WriteLine(version.Key);
    }

    Console.WriteLine("=== HuggingFace Test Complete ===");
  }
}
