// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
/// Represents a file associated with an artifact version.
/// </summary>
public record ArtifactFile {
  /// <summary>
  /// Gets or sets the URI of the file.
  /// </summary>
  public required string uri { get; set; }
  /// <summary>
  /// Gets or sets the folder where the file is located.
  /// </summary>
  public string folder { get; set; } = "";
}
