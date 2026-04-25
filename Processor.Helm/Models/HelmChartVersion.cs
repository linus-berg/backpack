// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Helm.Models;

/// <summary>
///   Represents a version of a Helm chart.
/// </summary>
public class HelmChartVersion {
  /// <summary>
  ///   Gets or sets the version string.
  /// </summary>
  public required string version { get; set; }
}