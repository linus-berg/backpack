// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Jetbrains.IDE.Models;

/// <summary>
///   Represents a JetBrains product.
/// </summary>
public class JetbrainsProduct {
  /// <summary>
  ///   Gets or sets the list of releases for the product.
  /// </summary>
  public required List<JetbrainsProductRelease> releases { get; init; }
}