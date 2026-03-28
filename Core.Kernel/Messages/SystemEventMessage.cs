using System;
using Core.Kernel.Models;

namespace Core.Kernel.Messages;

public class SystemEventMessage {
  public string source { get; set; }
  public string message { get; set; }
  public EventSeverity severity { get; set; } = EventSeverity.INFO;
  public string user { get; set; }
  public DateTime timestamp { get; set; } = DateTime.UtcNow;
}
