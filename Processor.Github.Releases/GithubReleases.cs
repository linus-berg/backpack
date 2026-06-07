// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Core.Kernel.Models;
using Library.Github;
using Library.Github.Models;

namespace Processor.Github.Releases;

/// <summary>
///   Implementation of GitHub releases processing.
/// </summary>
public class GithubReleases : IGithubReleases {
  private readonly IGithubClient gh_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="GithubReleases" /> class.
  /// </summary>
  /// <param name="gh">The GitHub client.</param>
  public GithubReleases(IGithubClient gh) {
    gh_ = gh;
  }

  /// <summary>
  ///   Processes the artifact to find GitHub releases.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    List<GithubRelease> releases = await gh_.GetReleases(artifact.id);
    List<Regex> files_regexp =
      artifact.config["files"].Split(";").Select(r => new Regex(r)).ToList();

    foreach (GithubRelease release in releases) {
      ArtifactVersion version = new() {
        version = release.tag_name
      };
      foreach (GithubReleaseAsset asset in release.assets) {
        foreach (Regex file_regexp in files_regexp) {
          if (file_regexp.IsMatch(asset.name)) {
            string url = asset.browser_download_url;
            version.AddFile(Path.GetFileName(url), url);
          }
        }
      }

      if (artifact.config.TryGetValue(
            "include_source",
            out string? include_source
          )) {
        if (bool.TryParse(include_source, out bool include) && include) {
          version.AddFile("source_tar", release.tarball_url);
        }
      }

      artifact.AddVersion(version);
    }

    return artifact;
  }
}