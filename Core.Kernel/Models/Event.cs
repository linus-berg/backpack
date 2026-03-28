using System;

namespace Core.Kernel.Models;

public enum EventSeverity {
  INFO,
  WARNING,
  ERROR,
  SUCCESS
}

public class Event {
  public string id { get; set; } = Guid.NewGuid().ToString();
  public DateTime timestamp { get; set; } = DateTime.UtcNow;
  public string source { get; set; }
  public string message { get; set; }
  public EventSeverity severity { get; set; } = EventSeverity.INFO;
  
  public string user { get; set; }
}
