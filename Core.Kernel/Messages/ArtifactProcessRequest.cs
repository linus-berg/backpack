// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;

namespace Core.Kernel.Messages;

/// <summary>
///   Represents a request to process a specific artifact.
/// </summary>
public class ArtifactProcessRequest {
  /// <summary>
  ///   Gets or sets the context ID associated with this process request.
  /// </summary>
  public Guid ctx { get; set; }

  /// <summary>
  ///   Gets or sets the artifact to be processed.
  /// </summary>
  public required Artifact artifact { get; set; }
}