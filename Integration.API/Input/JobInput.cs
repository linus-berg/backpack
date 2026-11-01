namespace Integration.API.Input;

/// <summary>
///   Starts a background job.
/// </summary>
public class JobInput {
  /// <summary><c>track</c> or <c>validate</c>.</summary>
  public required string kind { get; init; }

  /// <summary>One processor, or empty for every processor.</summary>
  public string? processor { get; init; }
}
