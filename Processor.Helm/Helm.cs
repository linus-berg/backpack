// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Processor.Helm.Models;
using RestSharp;

namespace Processor.Helm;

/// <summary>
///   Logic for processing Helm charts from Artifact Hub.
/// </summary>
public class Helm {
  private const string C_API_ = "https://artifacthub.io/api/v1/packages/helm";
  private readonly RestClient client_ = new(C_API_);
  private readonly ILogger<Helm> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Helm" /> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  public Helm(ILogger<Helm> logger) {
    logger_ = logger;
    AddApiKeyIfAvailable();
  }

  private void AddApiKeyIfAvailable() {
    string? api_key_id =
      Environment.GetEnvironmentVariable("ARTIFACTHUB_API_KEY_ID");
    string? api_key_secret =
      Environment.GetEnvironmentVariable("ARTIFACTHUB_API_KEY_SECRET");
    if (!string.IsNullOrEmpty(api_key_id) &&
        !string.IsNullOrEmpty(api_key_secret)) {
      client_.AddDefaultHeader("X-API-KEY-ID", api_key_id);
      client_.AddDefaultHeader("X-API-KEY-SECRET", api_key_secret);
    }
  }

  /// <summary>
  ///   Processes the artifact to find Helm chart versions and dependencies.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation.</returns>
  public async Task ProcessArtifact(Artifact artifact) {
    await ProcessVersions(artifact);
  }

  private async Task ProcessVersions(Artifact artifact) {
    HelmChartMetadata? metadata = await GetMetadata(artifact.id);
    if (metadata == null) {
      throw new ArtifactMetadataException($"Metadata not found {artifact.id}");
    }
    foreach (HelmChartVersion hv in metadata.available_versions) {
      if (artifact.HasVersion(hv.version)) {
        continue;
      }

      HelmChartVersionMetadata? vm = await GetVersionMetadata(artifact.id, hv.version);
      if (vm == null) {
        continue;
      }
      ArtifactVersion version = new();
      version.AddFile("chart", vm.content_url);
      version.version = vm.version;

      /* Add required containers */
      AddContainers(version, vm.containers_images);
      artifact.AddVersion(version);
      AddDependencies(artifact, vm.data);
    }
  }

  private void AddContainers(ArtifactVersion artifact_version,
                             IEnumerable<HelmChartContainerImage> images) {
    foreach (HelmChartContainerImage image in images) {
      artifact_version.AddFile(
        $"{image.image}",
        FixNaming(image.image),
        "container"
      );
    }
  }

  private static string FixNaming(string name) {
    return !HasHostname(name)
             ? $"docker://docker.io/{name}"
             : $"docker://{name}";
  }

  private static bool HasHostname(string name) {
    bool is_match = Regex.IsMatch(name, @"\w+\.\w+\/");
    return is_match;
  }

  private void AddDependencies(Artifact artifact, HelmChartData data) {
    if (data.dependencies == null) {
      return;
    }

    foreach (HelmChartDependency chart in data.dependencies) {
      TryAddDependency(artifact, chart);
    }
  }

  private bool TryAddDependency(Artifact artifact, HelmChartDependency chart) {
    try {
      AddDependency(artifact, chart);
    } catch (Exception e) {
      logger_.LogError(e, "Error adding dependencies");
      return false;
    }

    return true;
  }

  private bool AddDependency(Artifact artifact, HelmChartDependency chart) {
    if (string.IsNullOrEmpty(chart.repository)) {
      return false;
    }

    Uri uri = new(chart.repository);
    if (uri.Scheme == "file" ||
        string.IsNullOrEmpty(chart.artifacthub_repository_name)) {
      return false;
    }

    artifact.AddDependency(
      $"{chart.artifacthub_repository_name}/{chart.name}",
      artifact.processor
    );
    return true;
  }

  private async Task<HelmChartVersionMetadata?> GetVersionMetadata(string id, string version) {
    return await client_.GetAsync<HelmChartVersionMetadata>($"/{id}/{version}");
  }

  private async Task<HelmChartMetadata?> GetMetadata(string id) {
    return await client_.GetAsync<HelmChartMetadata>($"/{id}");
  }
}