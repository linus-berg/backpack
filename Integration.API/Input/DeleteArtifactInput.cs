namespace Integration.API.Input;

public class DeleteArtifactInput {
  public required string id { get; init; }
  public required string processor { get; init; }
}