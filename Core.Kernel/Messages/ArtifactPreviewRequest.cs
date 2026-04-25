namespace Core.Kernel.Messages;

public class ArtifactPreviewRequest {
  public required string id { get; set; }
  public required string processor { get; set; }
}