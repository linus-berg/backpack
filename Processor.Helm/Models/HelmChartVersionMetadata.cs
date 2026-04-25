// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Helm.Models;

/// <summary>
///   Represents metadata for a Helm chart.
/// </summary>
public class HelmChartVersionMetadata {
  /// <summary>
  ///   Initializes a new instance of the <see cref="HelmChartMetadata" /> class.
  /// </summary>
  public HelmChartVersionMetadata() {
    containers_images = new HashSet<HelmChartContainerImage>();
  }
  /// <summary>
  ///   Gets or sets the content URL.
  /// </summary>
  public required string content_url { get; set; }

  /// <summary>
  ///   Gets or sets the version string.
  /// </summary>
  public required string version { get; set; }

  /// <summary>
  ///   Gets or sets the chart data.
  /// </summary>
  public required HelmChartData data { get; set; }

  /// <summary>
  ///   Gets or sets the container images associated with the chart.
  /// </summary>
  public required IEnumerable<HelmChartContainerImage> containers_images { get; set; }
}