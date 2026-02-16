namespace Integration.API.Output;

public class ProcessorOutput {
  public string id { get; set; }
  public string config { get; set; }

  public string description { get; set; }
  public bool direct_collect { get; set; }
}