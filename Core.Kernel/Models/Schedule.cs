namespace Core.Kernel.Models;

public class Schedule {
  public string id { get; set; }
  public string processor { get; set; }
  public string cron { get; set; }
  public DateTime? last_run { get; set; }
  public DateTime? next_run { get; set; }
}