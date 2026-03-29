// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Processor.Pypi.Models;

/// <summary>
///   Represents the information about a PyPI package version.
/// </summary>
public class PypiInfo {
  /// <summary>
  ///   Gets or sets the list of required distributions (dependencies).
  /// </summary>
  public List<string>? requires_dist { get; set; }

  /// <summary>
  ///   Extracts dependency identifiers from the requires_dist list.
  /// </summary>
  /// <returns>A list of dependency project identifiers.</returns>
  public List<string> GetDependencies() {
    List<string> dependencies = new();
    if (requires_dist == null) {
      return dependencies;
    }

    foreach (string dist in requires_dist) {
      // requires_dist items look like:
      // "requests (>=2.25.1)"
      // "pywin32; sys_platform == 'win32'"
      // "numpy"

      // We want the first part, which is the package name.
      // We'll split by characters that signify the end of the name.
      string dependency =
        Regex.Split(dist, @"[\s(;<>=!~]").FirstOrDefault() ?? "";

      if (!string.IsNullOrWhiteSpace(dependency)) {
        dependencies.Add(dependency);
      }
    }

    return dependencies;
  }
}