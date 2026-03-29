using Core.Kernel.Models;

namespace Core.Kernel.Messages;

public class ArtifactPreviewResponse {
  public Artifact? artifact { get; set; }
  public string? error { get; set; }
}