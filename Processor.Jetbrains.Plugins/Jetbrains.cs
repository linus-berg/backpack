// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Processor.Jetbrains.Plugins.Models;
using RestSharp;

namespace Processor.Jetbrains.Plugins;

/// <summary>
///   Logic for processing JetBrains plugins.
/// </summary>
public class Jetbrains : IJetbrains {
  private const string C_API_ = "https://plugins.jetbrains.com";
  private readonly RestClient client_ = new(C_API_);

  /// <summary>
  ///   Processes the artifact to find JetBrains plugin updates.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    string id = GetPluginId(artifact.id);
    List<JetbrainsPluginUpdate>? updates = await GetUpdates(id);
    if (updates == null) {
      return artifact;
    }

    AddVersions(artifact, updates);

    return artifact;
  }

  private void AddVersions(Artifact artifact,
                           List<JetbrainsPluginUpdate> updates) {
    foreach (JetbrainsPluginUpdate update in updates) {
      ArtifactVersion version = new() {
        version = update.version
      };
      version.AddFile("plugin", $"{C_API_}/files/{update.file}");
      artifact.AddVersion(version);
    }
  }

  private async Task<List<JetbrainsPluginUpdate>?> GetUpdates(string id) {
    try {
      return await client_.GetAsync<List<JetbrainsPluginUpdate>>(
               $"/api/plugins/{id}/updates"
             );
    } catch (TimeoutException) {
      throw new ArtifactTimeoutException($"{id} timed out!");
    } catch (Exception) {
      throw new ArtifactMetadataException($"{id} metadata error!");
    }
  }

  private string GetPluginId(string full_id) {
    return full_id.Split('-')[0];
  }
}