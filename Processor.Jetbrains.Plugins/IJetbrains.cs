// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;

namespace Processor.Jetbrains.Plugins;

/// <summary>
/// Interface for JetBrains plugins processing.
/// </summary>
public interface IJetbrains {
  /// <summary>
  /// Processes the artifact to find JetBrains plugin updates.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public Task<Artifact> ProcessArtifact(Artifact artifact);
}