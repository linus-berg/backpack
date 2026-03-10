// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Processor.Npm.Models;
using RestSharp;

namespace Processor.Npm;

/// <summary>
/// Logic for processing NPM packages from the registry.
/// </summary>
public class Npm : INpm {
  private const string C_REGISTRY_ = "https://registry.npmjs.org/";
  private const string C_FILE_NAME_ = "tarball";
  private readonly RestClient client_ = new(C_REGISTRY_);
  private readonly ILogger<Npm> logger_;

  /// <summary>
  /// Initializes a new instance of the <see cref="Npm"/> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  public Npm(ILogger<Npm> logger) {
    logger_ = logger;
  }

  /// <summary>
  /// Processes the artifact to find NPM package versions and dependencies.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    Metadata? metadata = await GetMetadata(artifact.id);
    ProcessArtifactVersions(artifact, metadata);
    return artifact;
  }

  private void ProcessArtifactVersions(Artifact artifact, Metadata? metadata) {
    if (metadata?.versions == null) {
      return;
    }

    foreach (KeyValuePair<string, Package> kv in metadata.versions) {
      if (artifact.HasVersion(kv.Key)) {
        continue;
      }

      Package package = kv.Value;
      ArtifactVersion version = new() {
        version = kv.Key
      };
      version.AddFile(C_FILE_NAME_, package.dist.tarball);
      AddDependencies(artifact, package.dependencies);
      AddDependencies(artifact, package.peerDependencies);
      artifact.AddVersion(version);
    }
  }

  private void AddDependencies(Artifact artifact,
                               Dictionary<string, JsonElement>? dependencies) {
    if (dependencies == null) {
      return;
    }

    foreach (KeyValuePair<string, JsonElement> package in dependencies) {
      artifact.AddDependency(package.Key, artifact.processor);
    }
  }

  private async Task<Metadata?> GetMetadata(string id) {
    try {
      return await client_.GetAsync<Metadata>($"{id}/");
    } catch (TimeoutException ex) {
      logger_.LogError("Timeout error: {Exception}", ex.ToString());
      throw new ArtifactTimeoutException($"{id} timed out!");
    } catch (Exception ex) {
      logger_.LogError("Metadata error: {Exception}", ex.ToString());
      throw new ArtifactMetadataException($"{id} metadata error!");
    }
  }
}