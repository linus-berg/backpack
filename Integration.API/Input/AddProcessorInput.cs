namespace Integration.API.Input;

public class AddProcessorInput {
  public string processor_id { get; set; }
  public bool requires_approval { get; set; }
}