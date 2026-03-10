// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;

namespace Processor.Jetbrains.IDE;

/// <summary>
/// Interface for JetBrains IDE processing.
/// </summary>
public interface IJetbrains {
  /// <summary>
  /// Processes the artifact to find JetBrains IDE releases.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public Task<Artifact> ProcessArtifact(Artifact artifact);
}