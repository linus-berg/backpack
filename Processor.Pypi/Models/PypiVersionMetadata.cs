// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Pypi.Models;

/// <summary>
///   Represents the metadata for a specific version of a PyPI package.
/// </summary>
public class PypiVersionMetadata {
  /// <summary>
  ///   Gets or sets the information about the package version.
  /// </summary>
  public required PypiInfo info { get; set; }
}