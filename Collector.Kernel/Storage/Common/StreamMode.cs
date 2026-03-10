// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Collector.Kernel.Storage.Common;

/// <summary>
/// Specifies the mode for opening a file stream.
/// </summary>
public enum StreamMode {
  /// <summary>
  /// Open for reading.
  /// </summary>
  READ,
  /// <summary>
  /// Open for writing.
  /// </summary>
  WRITE
}
