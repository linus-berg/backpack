namespace Integration.API.Output;

public class ProcessorOutput {
  public required string id { get; set; }
  public required string config { get; set; }

  public required string description { get; set; }
  public bool direct_collect { get; set; }
  public bool requires_approval { get; set; }
  public bool multi_add { get; set; }
  public bool is_external { get; set; }
  public bool preview_enabled { get; set; }
}