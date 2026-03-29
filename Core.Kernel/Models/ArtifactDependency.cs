// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
///   Represents a dependency of an artifact on another artifact.
/// </summary>
public class ArtifactDependency {
  /// <summary>
  ///   Gets the unique identifier of the dependency.
  /// </summary>
  public required string id { get; init; }

  /// <summary>
  ///   Gets the name of the processor for the dependency.
  /// </summary>
  public required string processor { get; init; }

  /// <summary>
  ///   Gets or sets a dictionary of configuration settings for the dependency.
  /// </summary>
  public Dictionary<string, string> config { get; set; } = new();

  /// <summary>
  ///   Determines whether the specified object is equal to the current dependency.
  /// </summary>
  /// <param name="obj">The object to compare with the current dependency.</param>
  /// <returns>True if the objects are equal; otherwise, false.</returns>
  public override bool Equals(object? obj) {
    ArtifactDependency? dep = obj as ArtifactDependency;
    return dep != null && id.Equals(dep.id);
  }

  /// <summary>
  ///   Serves as the default hash function.
  /// </summary>
  /// <returns>A hash code for the current dependency.</returns>
  public override int GetHashCode() {
    return id.GetHashCode();
  }
}