// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
///   Provides a summary of an artifact, including counts of its versions and dependencies.
/// </summary>
public class ArtifactSummary {
  /// <summary>
  ///   Gets or sets the unique identifier for the artifact.
  /// </summary>
  public required string id { get; set; }

  /// <summary>
  ///   Gets or sets the name of the processor associated with this artifact.
  /// </summary>
  public required string processor { get; set; }

  /// <summary>
  ///   Gets or sets the filter used for selecting artifact versions.
  /// </summary>
  public required string filter { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether this is a root artifact.
  /// </summary>
  public bool root { get; set; }

  /// <summary>
  ///   Gets or sets a dictionary of configuration settings for this artifact.
  /// </summary>
  public required Dictionary<string, string> config { get; set; }

  /// <summary>
  ///   Gets or sets the total number of versions for this artifact.
  /// </summary>
  public int versions { get; set; }

  /// <summary>
  ///   Gets or sets the total number of dependencies for this artifact.
  /// </summary>
  public int dependencies { get; set; }
}