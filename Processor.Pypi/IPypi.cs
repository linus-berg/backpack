// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Models;

namespace Processor.Pypi;

/// <summary>
///   Interface for PyPI artifact processing.
/// </summary>
public interface IPypi {
  /// <summary>
  ///   Processes the artifact to find PyPI package versions and dependencies.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  Task<Artifact> ProcessArtifact(Artifact artifact);
}