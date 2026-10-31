// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Core.Kernel.Exceptions;

namespace Core.Kernel.Messages;

/// <summary>
///   Represents a request to collect an artifact from a specific location.
/// </summary>
public class ArtifactCollectRequest {
  /// <summary>
  ///   Gets or sets the location of the artifact (e.g., a URL).
  /// </summary>
  public required string location { get; set; }

  /// <summary>
  ///   Gets or sets the name of the module responsible for the collection.
  /// </summary>
  public required string module { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether to force the collection even if the artifact already exists.
  /// </summary>
  public bool force { get; set; } = false;

  /// <summary>
  ///   Determines the name of the collector module based on the location's URI scheme.
  /// </summary>
  /// <returns>The name of the collector module.</returns>
  /// <exception cref="InvalidCollectLocationException">
  ///   The location is not an absolute URL with a scheme.
  /// </exception>
  public string GetCollectorModule() {
    return GetCollectorModule(location);
  }

  /// <summary>
  ///   Determines the collector module for a location.
  /// </summary>
  /// <param name="location">The location, e.g. <c>git://github.com/owner/repo</c>.</param>
  /// <returns>The name of the collector module, e.g. <c>collector-git</c>.</returns>
  /// <exception cref="InvalidCollectLocationException">
  ///   The location is not an absolute URL with a scheme.
  /// </exception>
  public static string GetCollectorModule(string location) {
    if (!TryGetCollectorModule(location, out string? module)) {
      throw new InvalidCollectLocationException(location);
    }

    return module;
  }

  /// <summary>
  ///   Determines the collector module for a location without throwing.
  /// </summary>
  /// <remarks>
  ///   Requires <c>scheme://</c>. Without that check <c>new Uri</c> would also
  ///   accept e.g. <c>github.com:owner/repo</c> with the scheme
  ///   <c>github.com</c>, and the request would go to a queue no collector reads.
  /// </remarks>
  /// <param name="location">The location to check.</param>
  /// <param name="module">The collector module, when the location is valid.</param>
  /// <returns>True when the location is an absolute URL with a scheme.</returns>
  public static bool TryGetCollectorModule(string? location,
                                           [NotNullWhen(true)] out string?
                                             module) {
    module = null;
    if (string.IsNullOrWhiteSpace(location) || !location.Contains("://") ||
        !Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) ||
        string.IsNullOrEmpty(uri.Scheme)) {
      return false;
    }

    module = $"collector-{uri.Scheme}";
    return true;
  }
}