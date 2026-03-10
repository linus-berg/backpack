// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Collector.Kernel.Storage.Common;

/// <summary>
/// Represents criteria for searching files, combining a prefix and a regular expression pattern.
/// </summary>
public class SearchCriteria {
  /// <summary>
  /// Gets or sets the path prefix for the search.
  /// </summary>
  public string prefix { get; set; }
  /// <summary>
  /// Gets or sets the regular expression pattern for matching file names.
  /// </summary>
  public Regex pattern { get; set; }
}
