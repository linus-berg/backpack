namespace Core.Kernel.Models;

public class PendingArtifact {
  public required string id { get; set; }
  public required string processor { get; set; }
  public required string filter { get; set; } = string.Empty;
  public required Dictionary<string, string> config { get; set; }
  public required string requested_by { get; set; }
  public DateTime timestamp { get; set; } = DateTime.UtcNow;
}