// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Helm.Models;

/// <summary>
///   Represents data associated with a Helm chart version.
/// </summary>
public class HelmChartData {
  /// <summary>
  ///   Gets or sets the version string.
  /// </summary>
  public string version { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether this is a prerelease version.
  /// </summary>
  public bool prerelease { get; set; }

  /// <summary>
  ///   Gets or sets the dependencies of the Helm chart.
  /// </summary>
  public IEnumerable<HelmChartDependency> dependencies { get; set; }
}