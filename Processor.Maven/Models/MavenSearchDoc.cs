// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Maven.Models;

/// <summary>
///   Represents a document in a Maven search result.
/// </summary>
public class MavenSearchDoc {
  /// <summary>
  ///   Gets or sets the document identifier.
  /// </summary>
  public string id { get; set; }

  /// <summary>
  ///   Gets or sets the group identifier.
  /// </summary>
  public string g { get; set; }

  /// <summary>
  ///   Gets or sets the version string.
  /// </summary>
  public string v { get; set; }

  /// <summary>
  ///   Gets or sets the list of file extensions available for this version.
  /// </summary>
  public List<string> ec { get; set; }
}