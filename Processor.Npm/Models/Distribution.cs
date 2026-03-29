// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Npm.Models;

/// <summary>
///   Represents the distribution information of an NPM package version.
/// </summary>
public class Distribution {
  /// <summary>
  ///   Gets or sets the SHA sum of the package.
  /// </summary>
  public string shasum { get; set; }

  /// <summary>
  ///   Gets or sets the URL to the tarball.
  /// </summary>
  public string tarball { get; set; }
}