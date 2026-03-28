namespace Integration.API.Input;

public class UpdateProcessorInput {
  public string processor_id { get; set; }
  public string description { get; set; }
  public string config { get; set; }
  public bool direct_collect { get; set; }
}