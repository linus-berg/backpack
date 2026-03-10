// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Pypi.Models;

/// <summary>
/// Represents the metadata for a PyPI package including all releases.
/// </summary>
public class PypiMetadata {
  /// <summary>
  /// Gets or sets the general information for the latest version.
  /// </summary>
  public PypiInfo info { get; set; }

  /// <summary>
  /// Gets or sets the releases for the package, grouped by version.
  /// </summary>
  public Dictionary<string, List<PypiRelease>> releases { get; set; }

  /// <summary>
  /// Retrieves all versions that have valid releases.
  /// </summary>
  /// <returns>A dictionary of version strings and their corresponding list of valid releases.</returns>
  public Dictionary<string, List<PypiRelease>> GetAllValidReleases() {
    Dictionary<string, List<PypiRelease>> valid = new();

    foreach (KeyValuePair<string, List<PypiRelease>> kv in releases) {
      List<PypiRelease> versionReleases = kv.Value;
      string version = kv.Key;
      foreach (PypiRelease release in versionReleases) {
        if (!release.IsValid()) {
          continue;
        }

        if (!valid.ContainsKey(version)) {
          valid[version] = new List<PypiRelease>();
        }
        valid[version].Add(release);
      }
    }

    return valid;
  }
}
