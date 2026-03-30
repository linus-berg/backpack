namespace Integration.API.Input;

public class UpdateProcessorInput {
  public string processor_id { get; set; }
  public string description { get; set; }
  public string config { get; set; }
  public bool direct_collect { get; set; }
  public bool requires_approval { get; set; }
  public bool multi_add { get; set; }
  public bool is_external { get; set; }
  public bool preview_enabled { get; set; }
  }