namespace Integration.API.Input;

public class ArtifactValidationInput {
  public required string id { get; set; }
  public required string processor { get; set; }
  public bool force { get; set; }
}