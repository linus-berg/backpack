using System;
using System.Collections.Generic;

namespace Core.Kernel.Models;

public class PendingArtifact {
  public string id { get; set; }
  public string processor { get; set; }
  public string filter { get; set; }
  public Dictionary<string, string> config { get; set; }
  public string requested_by { get; set; }
  public DateTime timestamp { get; set; } = DateTime.UtcNow;
}
