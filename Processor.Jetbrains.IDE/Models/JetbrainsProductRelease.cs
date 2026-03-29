// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Jetbrains.IDE.Models;

/// <summary>
///   Represents a release of a JetBrains product.
/// </summary>
public class JetbrainsProductRelease {
  /// <summary>
  ///   Gets or sets the version string.
  /// </summary>
  public string version { get; set; }

  /// <summary>
  ///   Gets or sets the downloads available for this release, keyed by platform.
  /// </summary>
  public Dictionary<string, JetbrainsProductDownload> downloads { get; set; }
}