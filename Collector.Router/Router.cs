// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;
using Semver;

namespace Collector.Router;

/// <summary>
///   Consumer for artifact routing requests.
/// </summary>
public class Router : IConsumer<ArtifactRouteRequest> {
  private static readonly Predicate<string> S_NO_FILTER_ = s => true;

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactRouteRequest> context) {
    Artifact artifact = context.Message.artifact;

    Predicate<string> artifact_filter = CreateFilterFunction(artifact);

    foreach (KeyValuePair<string, ArtifactVersion> kv in artifact.versions) {
      bool collect = artifact_filter(kv.Key);

      if (!collect) {
        continue;
      }

      foreach (KeyValuePair<string, ArtifactFile> file in kv.Value.files) {
        await context.Collect(
          file.Value.uri,
          string.IsNullOrEmpty(file.Value.folder)
            ? artifact.processor
            : file.Value.folder
        );
      }
    }
  }

  /// <summary>
  ///   Creates a filter function for an artifact based on its filter type.
  /// </summary>
  /// <param name="artifact">The artifact containing filter criteria.</param>
  /// <returns>A predicate function for filtering versions.</returns>
  private static Predicate<string> CreateFilterFunction(Artifact artifact) {
    if (string.IsNullOrEmpty(artifact.filter)) {
      return S_NO_FILTER_;
    }

    switch (artifact.filter_type) {
      case ArtifactFilterType.SEMVER_RANGE:
        SemVersionRange version_range = SemVersionRange.Parse(artifact.filter);
        return s => {
          SemVersion version = SemVersion.Parse(s, SemVersionStyles.Any);
          return version_range.Contains(version);
        };
      case ArtifactFilterType.REGEX:
        goto default;
      default:
        // fallback to regex, for backwards compatibility
        Regex regex = new(artifact.filter);
        return s => regex.IsMatch(s);
    }
  }
}