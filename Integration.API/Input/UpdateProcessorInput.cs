namespace Integration.API.Input;

public class UpdateProcessorInput {
  public required string processor_id { get; init; }
  public required string description { get; init; }
  public required string config { get; init; }
  public required bool direct_collect { get; set; }
  public bool requires_approval { get; set; }
  public bool multi_add { get; set; }
  public bool is_external { get; set; }
  public bool preview_enabled { get; set; }
}