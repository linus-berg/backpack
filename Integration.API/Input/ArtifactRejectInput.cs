namespace Integration.API.Input;

public class ArtifactRejectInput {
  public required string id { get; set; }
  public required string processor { get; set; }

  /// <summary>
  ///   Optional explanation recorded in the event log so the requester can see
  ///   why their artifact was rejected.
  /// </summary>
  public string? reason { get; set; }
}
