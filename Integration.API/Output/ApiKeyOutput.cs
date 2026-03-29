namespace Integration.API.Output;

public class ApiKeyOutput {
  public string id { get; set; }
  public string name { get; set; }
  public string key_preview { get; set; }
  public bool is_admin { get; set; }
  public DateTime created_at { get; set; }
  public string created_by { get; set; }
}