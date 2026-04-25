namespace Integration.API.Output;

public class UserOutput {
  public required string name { get; set; }
  public List<string> roles { get; set; } = new();
}