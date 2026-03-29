// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Pypi.Models;

/// <summary>
///   Represents a release of a PyPI package.
/// </summary>
public class PypiRelease {
  /// <summary>
  ///   Gets or sets the filename of the release.
  /// </summary>
  public string filename { get; set; }

  /// <summary>
  ///   Gets or sets the URL for downloading the release.
  /// </summary>
  public string url { get; set; }

  /// <summary>
  ///   Gets or sets the type of the package (e.g., sdist, bdist_wheel).
  /// </summary>
  public string packagetype { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether the release has been yanked.
  /// </summary>
  public bool yanked { get; set; }

  /// <summary>
  ///   Determines if the release is valid for inclusion in the system.
  /// </summary>
  /// <returns>True if the release is valid; otherwise, false.</returns>
  public bool IsValid() {
    // Excluding macOS specific distributions if not relevant
    if (filename.Contains("macos")) {
      return false;
    }

    if (yanked) {
      return false;
    }

    // Example exclusion: avoid legacy windows installers
    return packagetype != "bdist_wininst";
  }
}