namespace Core.Kernel.Models;

public class NewsPost {
  public string id { get; set; } = Guid.NewGuid().ToString();
  public required string title { get; set; }
  public required string content { get; set; }
  public string author { get; set; } = "";
  public DateTime timestamp { get; set; } = DateTime.UtcNow;
}