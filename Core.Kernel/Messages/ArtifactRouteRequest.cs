// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;

namespace Core.Kernel.Messages;

/// <summary>
///   Represents a request to route an artifact for processing.
/// </summary>
public class ArtifactRouteRequest {
  /// <summary>
  ///   Gets or sets the artifact to be routed.
  /// </summary>
  public Artifact artifact { get; set; }
}