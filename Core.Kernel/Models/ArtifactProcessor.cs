// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
/// Represents an artifact processor and its configuration.
/// </summary>
public class ArtifactProcessor {
  /// <summary>
  /// Gets or sets the unique identifier for the artifact processor.
  /// </summary>
  public string id { get; set; }
  /// <summary>
  /// Gets or sets the configuration string for the processor.
  /// </summary>
  public string config { get; set; }
}
