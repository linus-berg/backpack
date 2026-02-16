namespace Integration.API.Input;

public class ArtifactValidationInput {
  public string id { get; set; }
  public string processor { get; set; }
  public bool force { get; set; }
}