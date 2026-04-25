namespace Core.Kernel.Models;

public class Schedule {
  public string? id { get; set; }
  public required string processor { get; set; }
  public required string cron { get; set; }
  public DateTime? last_run { get; set; }
  public DateTime? next_run { get; set; }
}