using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using Cronos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SchedulerController : ControllerBase {
  private readonly IArtifactService aps_;
  private readonly ICoreDatabase database_;
  private readonly IEventService event_service_;

  public SchedulerController(ICoreDatabase database, IArtifactService aps,
                             IEventService event_service) {
    database_ = database;
    aps_ = aps;
    event_service_ = event_service;
  }

  [HttpGet]
  public async Task<IEnumerable<Schedule>> Get() {
    List<Schedule> schedules = (await database_.GetSchedules()).ToList();
    foreach (Schedule schedule in schedules) {
      try {
        CronExpression expression = CronExpression.Parse(
          schedule.cron,
          CronFormat.IncludeSeconds
        );
        DateTime? next = expression.GetNextOccurrence(DateTime.UtcNow);
        if (next.HasValue) {
          schedule.next_run = next.Value;
        }
      } catch (Exception) {
        // Log or handle invalid cron
      }
    }

    return schedules;
  }

  [HttpPost("trigger/{processor}")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Trigger(string processor) {
    await aps_.Track(processor);
    await event_service_.LogEvent(
      "API",
      $"Manual track triggered for processor: {processor}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    return Ok(
      new {
        Message = $"Sync triggered for {processor}"
      }
    );
  }

  [HttpPost]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Update([FromBody] Schedule schedule) {
    await database_.UpdateSchedule(schedule);
    await event_service_.LogEvent(
      "API",
      $"Schedule updated for {schedule.processor}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    return Ok(schedule);
  }
}