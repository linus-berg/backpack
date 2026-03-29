// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Messages;

/// <summary>
///   Represents a request to collect an artifact from a specific location.
/// </summary>
public class ArtifactCollectRequest {
  /// <summary>
  ///   Gets or sets the location of the artifact (e.g., a URL).
  /// </summary>
  public string location { get; set; }

  /// <summary>
  ///   Gets or sets the name of the module responsible for the collection.
  /// </summary>
  public string module { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether to force the collection even if the artifact already exists.
  /// </summary>
  public bool force { get; set; } = false;

  /// <summary>
  ///   Determines the name of the collector module based on the location's URI scheme.
  /// </summary>
  /// <returns>The name of the collector module.</returns>
  public string GetCollectorModule() {
    Uri uri = new(location);
    string scheme = uri.Scheme;
    return $"collector-{scheme}";
  }
}