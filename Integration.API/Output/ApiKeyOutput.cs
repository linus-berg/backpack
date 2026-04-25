namespace Integration.API.Output;

public class ApiKeyOutput {
  public required string id { get; set; }
  public required string name { get; set; }
  public required string key_preview { get; set; }
  public bool is_admin { get; set; }
  public DateTime created_at { get; set; }
  public required string created_by { get; set; }
}