// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
///   Represents a fault that occurred during artifact processing.
/// </summary>
public class ArtifactProcessingFault {
  /// <summary>
  ///   Gets or sets the unique identifier for the fault.
  /// </summary>
  public int id { get; set; }

  /// <summary>
  ///   Gets or sets the name of the fault.
  /// </summary>
  public string name { get; set; }

  /// <summary>
  ///   Gets or sets the name of the processor that encountered the fault.
  /// </summary>
  public string processor { get; set; }

  /// <summary>
  ///   Gets or sets the time when the fault occurred.
  /// </summary>
  public DateTime time { get; set; }
}