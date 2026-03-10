// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Jetbrains.IDE.Models;

/// <summary>
/// Represents a download link for a JetBrains product release.
/// </summary>
public class JetbrainsProductDownload {
  /// <summary>
  /// Gets or sets the download URL.
  /// </summary>
  public string link { get; set; }
}