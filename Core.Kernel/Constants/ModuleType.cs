// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Constants;

/// <summary>
/// Specifies the type of module within the system.
/// </summary>
public enum ModuleType {
  /// <summary>
  /// A processor module.
  /// </summary>
  PROCESSOR,
  /// <summary>
  /// A collector module.
  /// </summary>
  COLLECTOR,
  /// <summary>
  /// A tracker module.
  /// </summary>
  TRACKER,
  /// <summary>
  /// A core system module.
  /// </summary>
  CORE
}
