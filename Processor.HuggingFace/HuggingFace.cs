// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Processor.HuggingFace.Models;
using RestSharp;

namespace Processor.HuggingFace;

/// <summary>
///   Logic for processing HuggingFace models from the hub.
/// </summary>
public class HuggingFace : IHuggingFace {
  private const string C_HUB_API_ = "https://huggingface.co/api/";

  private const string C_RESOLVE_URL_ =
    "https://huggingface.co/{0}/resolve/{1}/{2}";

  private readonly IRestClient client_;
  private readonly ILogger<HuggingFace> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="HuggingFace" /> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  /// <param name="client">The optional RestClient instance.</param>
  public HuggingFace(ILogger<HuggingFace> logger, IRestClient? client = null) {
    logger_ = logger;
    client_ = client ?? new RestClient(C_HUB_API_);
  }

  /// <summary>
  ///   Processes the artifact to find HuggingFace model versions and files.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    HuggingFaceModel? metadata = await GetMetadata(artifact.id);
    ProcessArtifactVersions(artifact, metadata);
    return artifact;
  }

  private void ProcessArtifactVersions(Artifact artifact,
                                       HuggingFaceModel? metadata) {
    if (metadata == null || string.IsNullOrEmpty(metadata.sha)) {
      return;
    }

    /* HuggingFace models always replace the previous version. */
    artifact.versions.Clear();

    ArtifactVersion version = new() {
      version = metadata.sha
    };

    if (metadata.siblings != null) {
      foreach (HuggingFaceSibling sibling in metadata.siblings) {
        // Use the custom hf:// protocol to trigger the HuggingFace collector.
        // Format: hf://{modelId}/{filename}?revision={sha}&modelId={modelId}
        // We include modelId in query because the URI Host/Path split is ambiguous for nested files.
        string url =
          $"hf://{metadata.id}/{sibling.rfilename}?revision={metadata.sha}&modelId={metadata.id}";
        version.AddFile(sibling.rfilename, url);
      }
    }

    artifact.AddVersion(version);
  }

  private async Task<HuggingFaceModel?> GetMetadata(string id) {
    try {
      RestRequest request = new($"models/{id}");
      RestResponse<HuggingFaceModel> response =
        await client_.ExecuteAsync<HuggingFaceModel>(request);
      return response.Data;
    } catch (TimeoutException ex) {
      logger_.LogError("Timeout error: {Exception}", ex.ToString());
      throw new ArtifactTimeoutException($"{id} timed out!");
    } catch (Exception ex) {
      logger_.LogError("Metadata error: {Exception}", ex.ToString());
      throw new ArtifactMetadataException($"{id} metadata error!");
    }
  }
}