// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MavenNet.Models;
using Artifact = Core.Kernel.Models.Artifact;

namespace Processor.Maven;

/// <summary>
///   Interface for Maven artifact processing.
/// </summary>
public interface IMaven {
  /// <summary>
  ///   Processes the artifact to find Maven versions and dependencies.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public Task<Artifact> ProcessArtifact(Artifact artifact);

  /// <summary>
  ///   Gets metadata for a Maven artifact.
  /// </summary>
  /// <param name="g">The group identifier.</param>
  /// <param name="id">The artifact identifier.</param>
  /// <returns>A task that represents the metadata retrieval operation.</returns>
  public Task<Metadata> GetMetadata(string g, string id);

  /// <summary>
  ///   Searches Maven for versions and files of an artifact.
  /// </summary>
  /// <param name="g">The group identifier.</param>
  /// <param name="id">The artifact identifier.</param>
  /// <returns>A task that represents the search operation, containing a dictionary of versions and their file extensions.</returns>
  public Task<Dictionary<string, List<string>>>
    SearchMaven(string g, string id);

  /// <summary>
  ///   Gets the POM (Project Object Model) for a specific version of a Maven artifact.
  /// </summary>
  /// <param name="g">The group identifier.</param>
  /// <param name="id">The artifact identifier.</param>
  /// <param name="v">The version string.</param>
  /// <returns>A task that represents the POM retrieval operation.</returns>
  public Task<Project> GetPom(string g, string id, string v);
}