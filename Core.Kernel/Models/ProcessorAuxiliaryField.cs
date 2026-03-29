// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
///   Represents an auxiliary configuration field for a processor.
/// </summary>
public class ProcessorAuxiliaryField {
  /// <summary>
  ///   Gets or sets the configuration key.
  /// </summary>
  public string key { get; set; }

  /// <summary>
  ///   Gets or sets the data type of the configuration field.
  /// </summary>
  public string type { get; set; }

  /// <summary>
  ///   Gets or sets the display name of the configuration field.
  /// </summary>
  public string name { get; set; }

  /// <summary>
  ///   Gets or sets the placeholder text for the configuration field.
  /// </summary>
  public string placeholder { get; set; }
}