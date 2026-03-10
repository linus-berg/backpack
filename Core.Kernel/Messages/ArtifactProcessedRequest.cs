// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;

namespace Core.Kernel.Messages;

/// <summary>
/// Represents a notification that an artifact has been processed, potentially including further collection requests.
/// </summary>
public class ArtifactProcessedRequest {
  /// <summary>
  /// Initializes a new instance of the <see cref="ArtifactProcessedRequest"/> class.
  /// </summary>
  public ArtifactProcessedRequest() {
    collect_requests = new List<ArtifactCollectRequest>();
  }

  /// <summary>
  /// Gets or sets a list of additional collection requests identified during processing.
  /// </summary>
  public List<ArtifactCollectRequest> collect_requests { get; set; }

  /// <summary>
  /// Gets or sets the context ID associated with the processing task.
  /// </summary>
  public Guid context { get; set; }
  /// <summary>
  /// Gets or sets the processed artifact.
  /// </summary>
  public Artifact artifact { get; set; }

  /// <summary>
  /// Adds a new collection request to the list of requests.
  /// </summary>
  /// <param name="location">The location of the artifact to collect.</param>
  /// <param name="module">The name of the module to handle the collection.</param>
  /// <param name="force">Whether to force the collection.</param>
  public void AddCollectRequest(string location, string module,
                                bool force = false) {
    collect_requests.Add(
      new ArtifactCollectRequest {
        module = module,
        location = location,
        force = force
      }
    );
  }
}
