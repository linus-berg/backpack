// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Processor.Npm.Models;

/// <summary>
///   Represents an NPM package version's metadata.
/// </summary>
public class Package {
  /// <summary>
  ///   Gets or sets the dependencies of the package.
  /// </summary>
  public Dictionary<string, JsonElement>? dependencies { get; set; }

  /// <summary>
  ///   Gets or sets the peer dependencies of the package.
  /// </summary>
  public Dictionary<string, JsonElement>? peerDependencies { get; set; }

  /// <summary>
  ///   Gets or sets the development dependencies of the package.
  /// </summary>
  public Dictionary<string, JsonElement>? devDependencies { get; set; }

  /// <summary>
  ///   Gets or sets the distribution information.
  /// </summary>
  public Distribution dist { get; set; }
}