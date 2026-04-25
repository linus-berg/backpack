// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Processor.Pypi.Models;
using RestSharp;

namespace Processor.Pypi;

/// <summary>
///   Logic for processing PyPI packages from the repository.
/// </summary>
public class Pypi : IPypi {
  private const string C_REGISTRY_ = "https://pypi.org/";
  private readonly RestClient client_ = new(C_REGISTRY_);
  private readonly ILogger<Pypi> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Pypi" /> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  public Pypi(ILogger<Pypi> logger) {
    logger_ = logger;
  }

  /// <summary>
  ///   Processes the artifact to find PyPI package versions and dependencies.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    PypiMetadata? metadata = await GetMetadata(artifact.id);

    if (metadata == null) {
      throw new ArtifactMetadataException($"Metadata is null {artifact.id}");
    }
    
    Dictionary<string, List<PypiRelease>> versions =
      metadata.GetAllValidReleases();

    foreach (KeyValuePair<string, List<PypiRelease>> kv in versions) {
      string version_str = kv.Key;
      List<PypiRelease> releases = kv.Value;

      if (artifact.HasVersion(version_str)) {
        continue;
      }

      ArtifactVersion a_version = new() {
        version = version_str
      };

      foreach (PypiRelease release in releases) {
        a_version.AddFile(release.filename, release.url);
      }

      // Fetch version-specific metadata for dependencies
      try {
        PypiVersionMetadata? version_metadata =
          await GetVersionMetadata(artifact.id, version_str);
        if (version_metadata?.info != null) {
          List<string> dependencies = version_metadata.info.GetDependencies();
          foreach (string dependency in dependencies) {
            artifact.AddDependency(dependency, artifact.processor);
          }
        }
      } catch (Exception ex) {
        logger_.LogWarning(
          "Could not fetch version-specific metadata for {Id} {Version}: {Error}",
          artifact.id,
          version_str,
          ex.Message
        );
      }

      artifact.AddVersion(a_version);
    }

    return artifact;
  }

  private async Task<PypiMetadata?> GetMetadata(string id) {
    try {
      return await client_.GetAsync<PypiMetadata>($"pypi/{id}/json");
    } catch (TimeoutException ex) {
      logger_.LogError(
        "Timeout error fetching metadata for {Id}: {Exception}",
        id,
        ex.ToString()
      );
      throw new ArtifactTimeoutException($"{id} timed out!");
    } catch (Exception ex) {
      logger_.LogError(
        "Metadata error for {Id}: {Exception}",
        id,
        ex.ToString()
      );
      throw new ArtifactMetadataException($"{id} metadata error!");
    }
  }

  private async Task<PypiVersionMetadata?> GetVersionMetadata(
    string id, string version) {
    try {
      return await client_.GetAsync<PypiVersionMetadata>(
               $"pypi/{id}/{version}/json"
             );
    } catch (Exception ex) {
      logger_.LogDebug(
        "Error fetching version metadata for {Id} {Version}: {Exception}",
        id,
        version,
        ex.ToString()
      );
      return null;
    }
  }
}