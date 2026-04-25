namespace Integration.API.Input;

public class ArtifactInput {
  public required string id { get; set; }
  public required string processor { get; set; }
  public required string filter { get; set; }
  public required Dictionary<string, string> config { get; init; }
}