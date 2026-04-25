// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
///   Represents a processor module within the system.
/// </summary>
public class Processor {
  /// <summary>
  ///   Gets or sets the unique identifier for the processor.
  /// </summary>
  public required string id { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether this processor supports direct collection.
  /// </summary>
  public bool direct_collect { get; set; }

  /// <summary>
  ///   Gets or sets a description of the processor.
  /// </summary>
  public string description { get; set; } = string.Empty;

  /// <summary>
  ///   Gets or sets a value indicating whether this processor requires approval for new artifacts.
  /// </summary>
  public bool requires_approval { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether this processor supports multi-add.
  /// </summary>
  public bool multi_add { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether this processor is handled externally.
  /// </summary>
  public bool is_external { get; set; } = false;

  /// <summary>
  ///   Gets or sets a value indicating whether the preview function is available for this processor.
  /// </summary>
  public bool preview_enabled { get; set; } = true;

  /// <summary>
  ///   Gets or sets a dictionary of auxiliary configuration fields for the processor.
  /// </summary>
  public Dictionary<string, ProcessorAuxiliaryField> config { get; set; } =
    new Dictionary<string, ProcessorAuxiliaryField>();
}