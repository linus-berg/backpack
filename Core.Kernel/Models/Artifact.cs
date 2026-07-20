// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
///   Represents an artifact within the system, including its versions, dependencies, and configuration.
/// </summary>
public class Artifact {
  /// <summary>
  ///   Initializes a new instance of the <see cref="Artifact" /> class.
  /// </summary>
  public Artifact() {
    versions = new Dictionary<string, ArtifactVersion>();
    dependencies = new HashSet<ArtifactDependency>();
    config = new Dictionary<string, string>();
  }

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
  public string filter { get; set; }

  /// <summary>
  ///   Gets or sets the type of filter applied to the artifact.
  /// </summary>
  public ArtifactFilterType filter_type { get; set; } =
    ArtifactFilterType.REGEX;

  /// <summary>
  ///   Gets or sets the current status of the artifact.
  /// </summary>
  public ArtifactStatus status { get; set; } = ArtifactStatus.PROCESSING;

  /// <summary>
  ///   Gets or sets a value indicating whether this is a root artifact.
  /// </summary>
  public bool root { get; set; } = false;

  /// <summary>
  ///   Gets or sets a dictionary of versions for this artifact, keyed by version string.
  /// </summary>
  public Dictionary<string, ArtifactVersion> versions { get; set; }

  /// <summary>
  ///   Gets or sets a dictionary of configuration settings for this artifact.
  /// </summary>
  public Dictionary<string, string> config { get; set; }

  /// <summary>
  ///   Gets or sets a set of dependencies for this artifact.
  /// </summary>
  public HashSet<ArtifactDependency> dependencies { get; set; }

  /// <summary>
  ///   Adds a dependency to the artifact.
  /// </summary>
  /// <param name="id">The identifier of the dependency.</param>
  /// <param name="processor">The processor for the dependency.</param>
  /// <returns>The newly created <see cref="ArtifactDependency" />.</returns>
  public ArtifactDependency AddDependency(string id, string processor) {
    ArtifactDependency dep = new() {
      id = id,
      processor = processor
    };
    dependencies.Add(dep);
    return dep;
  }

  /// <summary>
  ///   Adds a version to the artifact.
  /// </summary>
  /// <param name="version">The version to add.</param>
  /// <returns>True if the version was added; false if it already exists.</returns>
  public bool AddVersion(ArtifactVersion version) {
    return versions.TryAdd(version.version, version);
  }

  /// <summary>
  ///   Checks if the artifact has a specific version.
  /// </summary>
  /// <param name="version">The version string to check.</param>
  /// <returns>True if the version exists; otherwise, false.</returns>
  public bool HasVersion(string version) {
    return versions.ContainsKey(version);
  }
}