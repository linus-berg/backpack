// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Jetbrains.Plugins.Models;

/// <summary>
///   Represents an update for a JetBrains plugin.
/// </summary>
public record JetbrainsPluginUpdate {
  /// <summary>
  ///   Gets the update identifier.
  /// </summary>
  public int id { get; init; }

  /// <summary>
  ///   Gets the plugin identifier.
  /// </summary>
  public int pluginId { get; init; }

  /// <summary>
  ///   Gets the version string.
  /// </summary>
  public string version { get; init; }

  /// <summary>
  ///   Gets the file path for the update.
  /// </summary>
  public string file { get; init; }
}