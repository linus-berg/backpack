// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Helm.Models;

/// <summary>
///   Represents a container image associated with a Helm chart.
/// </summary>
public class HelmChartContainerImage {
  /// <summary>
  ///   Gets or sets the image identifier.
  /// </summary>
  public required string image { get; set; }
}