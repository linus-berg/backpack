// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Processor.Jetbrains.IDE.Models;
using RestSharp;

namespace Processor.Jetbrains.IDE;

/// <summary>
/// Logic for processing JetBrains IDE products.
/// </summary>
public class Jetbrains : IJetbrains {
  private const string C_API_ = "https://data.services.jetbrains.com";
  private readonly RestClient client_ = new(C_API_);

  /// <summary>
  /// Processes the artifact to find JetBrains IDE releases.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    string id = artifact.id;
    JetbrainsProduct product = await GetProduct(id);
    AddVersions(artifact, product);

    return artifact;
  }

  private void AddVersions(Artifact artifact, JetbrainsProduct product) {
    foreach (JetbrainsProductRelease release in product.releases) {
      Dictionary<string, JetbrainsProductDownload>
        downloads = release.downloads;
      if (downloads.TryGetValue(
            "linux",
            out JetbrainsProductDownload? download
          )) {
        ArtifactVersion version = new() {
          version = release.version
        };
        version.AddFile("linux", download.link);
        artifact.AddVersion(version);
      }
    }
  }

  private async Task<JetbrainsProduct> GetProduct(string id) {
    try {
      List<JetbrainsProduct>? products =
        await client_.GetJsonAsync<List<JetbrainsProduct>>(
          $"/products?code={id}&release.type=release"
        );

      if (products == null || products.Count == 0) {
        throw new ArtifactMetadataException($"{id} had no products in list");
      }

      return products[0];
    } catch (TimeoutException ex) {
      throw new ArtifactTimeoutException($"{id} timed out!");
    } catch (Exception ex) {
      throw new ArtifactMetadataException($"{id} metadata error!");
    }
  }
}