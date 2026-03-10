// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Npm.Models;

/// <summary>
/// Represents the metadata of an NPM package including all versions.
/// </summary>
public class Metadata {
  /// <summary>
  /// Gets or sets the versions of the package.
  /// </summary>
  public Dictionary<string, Package> versions { get; set; }
}