// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Helm.Models;

/// <summary>
///   Represents a Helm chart repository.
/// </summary>
public class HelmChartRepository {
  /// <summary>
  ///   Gets or sets the name of the repository.
  /// </summary>
  public string name { get; set; }
}