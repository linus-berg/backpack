namespace Core.Kernel.Models;

public class Event {
  public string id { get; set; } = Guid.NewGuid().ToString();
  public DateTime timestamp { get; set; } = DateTime.UtcNow;
  public required string source { get; set; }
  public required string message { get; set; }
  public EventSeverity severity { get; set; } = EventSeverity.INFO;

  public required string user { get; set; }
}