// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Maven.Models;

/// <summary>
/// Represents the response body of a Maven search result.
/// </summary>
public class MavenSearchResponse {
  /// <summary>
  /// Gets or sets the total number of documents found.
  /// </summary>
  public int numFound { get; set; }

  /// <summary>
  /// Gets or sets the start index of the results.
  /// </summary>
  public int start { get; set; }

  /// <summary>
  /// Gets or sets the list of documents found.
  /// </summary>
  public List<MavenSearchDoc> docs { get; set; }
}