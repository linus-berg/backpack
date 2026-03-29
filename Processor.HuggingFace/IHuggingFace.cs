using Core.Kernel.Models;

namespace Processor.HuggingFace;

/// <summary>
///   Interface for HuggingFace model processing.
/// </summary>
public interface IHuggingFace {
  /// <summary>
  ///   Processes the artifact to find HuggingFace model versions and files.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public Task<Artifact> ProcessArtifact(Artifact artifact);
}