// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;

namespace Core.Kernel.Messages;

/// <summary>
/// Represents a request to ingest an artifact into the system.
/// </summary>
public class ArtifactIngestRequest {
  /// <summary>
  /// Gets or sets the artifact to be ingested.
  /// </summary>
  public Artifact artifact { get; set; }
}
