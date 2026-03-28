using System;

namespace Core.Kernel.Models;

public class ApiKey {
  public string id { get; set; } = Guid.NewGuid().ToString();
  public string name { get; set; } = "";
  public string key { get; set; } = "";
  public bool is_admin { get; set; } = false;
  public DateTime created_at { get; set; } = DateTime.UtcNow;
  public string created_by { get; set; } = "";
}
