// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Maven.Models;

/// <summary>
/// Represents a Maven search result.
/// </summary>
public class MavenSearch {
  /// <summary>
  /// Gets or sets the search response.
  /// </summary>
  public MavenSearchResponse response { get; set; }
}