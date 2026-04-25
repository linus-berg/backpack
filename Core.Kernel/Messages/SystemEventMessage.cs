using Core.Kernel.Models;

namespace Core.Kernel.Messages;

public class SystemEventMessage {
  public required string source { get; init; }
  public required string message { get; init; }
  public EventSeverity severity { get; init; } = EventSeverity.INFO;
  public required string user { get; init; }
  public DateTime timestamp { get; init; } = DateTime.UtcNow;
}