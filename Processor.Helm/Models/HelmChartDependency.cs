// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Helm.Models;

/// <summary>
///   Represents a dependency of a Helm chart.
/// </summary>
public class HelmChartDependency {
  /// <summary>
  ///   Gets or sets the name of the dependency.
  /// </summary>
  public string name { get; set; }

  /// <summary>
  ///   Gets or sets the repository URL of the dependency.
  /// </summary>
  public string repository { get; set; }

  /// <summary>
  ///   Gets or sets the Artifact Hub repository name.
  /// </summary>
  public string artifacthub_repository_name { get; set; }
}