namespace Processor.Terraform.Models;

public class ProviderVersionMetadata {
  public required string download_url { get; set; }
  public required string shasums_url { get; set; }
  public required string shasums_signature_url { get; set; }
}