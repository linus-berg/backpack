// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
/// Specifies the type of filter used for artifact versions.
/// </summary>
public enum ArtifactFilterType {
  /// <summary>
  /// Regular expression filter.
  /// </summary>
  REGEX = 0,
  /// <summary>
  /// Semantic versioning range filter.
  /// </summary>
  SEMVER_RANGE = 1
}
