namespace Integration.API.Output;

/// <summary>
///   How many artifacts a processor has, for overviews that only need the
///   numbers (GET /api/artifact/counts).
/// </summary>
public class ArtifactCountOutput {
  /// <summary>
  ///   The processor ID.
  /// </summary>
  public required string processor { get; init; }

  /// <summary>
  ///   Artifacts someone added (root artifacts).
  /// </summary>
  public long roots { get; init; }

  /// <summary>
  ///   All artifacts, including those only there as dependencies.
  /// </summary>
  public long total { get; init; }
}
