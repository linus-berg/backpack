// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
/// Represents a specific version of an artifact and its associated files.
/// </summary>
public class ArtifactVersion {
  /// <summary>
  /// Gets or sets the version string.
  /// </summary>
  public string version { get; set; } = "-";

  /// <summary>
  /// Gets or sets the current status of this artifact version.
  /// </summary>
  public ArtifactVersionStatus status { get; set; } =
    ArtifactVersionStatus.SENT_FOR_COLLECTION;

  /// <summary>
  /// Gets or sets a dictionary of files associated with this version, keyed by file name.
  /// </summary>
  public Dictionary<string, ArtifactFile> files { get; set; } = new();

  /// <summary>
  /// Adds a file to this artifact version.
  /// </summary>
  /// <param name="name">The name of the file.</param>
  /// <param name="uri">The URI of the file.</param>
  /// <param name="folder">The folder where the file is located.</param>
  public void AddFile(string name, string uri, string folder = "") {
    files[name] = new ArtifactFile {
      uri = uri,
      folder = folder
    };
  }
}
