// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Infrastructure.Models;

/// <summary>
///   Provides a summary of an artifact, including counts of its versions and dependencies.
/// </summary>
public class ArtifactSummary {
  /// <summary>
  ///   Gets or sets the unique identifier for the artifact.
  /// </summary>
  public string id { get; set; }

  /// <summary>
  ///   Gets or sets the name of the processor associated with this artifact.
  /// </summary>
  public string processor { get; set; }

  /// <summary>
  ///   Gets or sets the filter used for selecting artifact versions.
  /// </summary>
  public string filter { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether this is a root artifact.
  /// </summary>
  public bool root { get; set; }

  /// <summary>
  ///   Gets or sets a dictionary of configuration settings for this artifact.
  /// </summary>
  public Dictionary<string, string> config { get; set; }

  /// <summary>
  ///   Gets or sets the total number of versions for this artifact.
  /// </summary>
  public int versions { get; set; }

  /// <summary>
  ///   Gets or sets the total number of dependencies for this artifact.
  /// </summary>
  public int dependencies { get; set; }
}