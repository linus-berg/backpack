// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Helm.Models;

/// <summary>
///   Represents metadata for a Helm chart.
/// </summary>
public class HelmChartMetadata {
  /// <summary>
  ///   Initializes a new instance of the <see cref="HelmChartMetadata" /> class.
  /// </summary>
  public HelmChartMetadata() {
    available_versions = new HashSet<HelmChartVersion>();
    containers_images = new HashSet<HelmChartContainerImage>();
  }

  /// <summary>
  ///   Gets or sets the name of the Helm chart.
  /// </summary>
  public string name { get; set; }

  /// <summary>
  ///   Gets or sets the package identifier.
  /// </summary>
  public string package_id { get; set; }

  /// <summary>
  ///   Gets or sets the content URL.
  /// </summary>
  public string content_url { get; set; }

  /// <summary>
  ///   Gets or sets the version string.
  /// </summary>
  public string version { get; set; }

  /// <summary>
  ///   Gets or sets the repository information.
  /// </summary>
  public HelmChartRepository repository { get; set; }

  /// <summary>
  ///   Gets or sets the chart data.
  /// </summary>
  public HelmChartData data { get; set; }

  /// <summary>
  ///   Gets or sets the available versions.
  /// </summary>
  public IEnumerable<HelmChartVersion> available_versions { get; set; }

  /// <summary>
  ///   Gets or sets the container images associated with the chart.
  /// </summary>
  public IEnumerable<HelmChartContainerImage> containers_images { get; set; }
}