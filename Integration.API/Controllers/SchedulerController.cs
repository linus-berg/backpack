using Core.Kernel;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Core.Services;
using Cronos;
using Wolverine;
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
  private readonly IMessageBus publish_endpoint_;

  public SchedulerController(ICoreDatabase database, IArtifactService aps,
                             IEventService event_service,
                             IMessageBus publish_endpoint) {
    database_ = database;
    aps_ = aps;
    event_service_ = event_service;
    publish_endpoint_ = publish_endpoint;
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

  [HttpPut]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Update([FromBody] Schedule schedule) {
    if (string.IsNullOrEmpty(schedule.processor) ||
        string.IsNullOrEmpty(schedule.cron)) {
      return BadRequest("Processor and Cron are required");
    }

    await database_.UpdateSchedule(schedule);
    await event_service_.LogEvent(
      "API",
      $"Schedule updated for {schedule.processor}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    await publish_endpoint_.PublishAsync(new ReloadSchedulesRequest());
    return Ok(schedule);
  }

  [HttpPost]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Add([FromBody] Schedule schedule) {
    if (string.IsNullOrEmpty(schedule.processor) ||
        string.IsNullOrEmpty(schedule.cron)) {
      return BadRequest("Processor and Cron are required");
    }

    schedule.id ??= Guid.NewGuid().ToString();
    await database_.AddSchedule(schedule);
    await event_service_.LogEvent(
      "API",
      $"Schedule added for {schedule.processor}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    await publish_endpoint_.PublishAsync(new ReloadSchedulesRequest());
    return Ok(schedule);
  }

  [HttpDelete("{id}")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Delete(string id) {
    bool deleted = await database_.DeleteSchedule(id);
    if (!deleted) {
      return NotFound();
    }

    await event_service_.LogEvent(
      "API",
      $"Schedule deleted: {id}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    await publish_endpoint_.PublishAsync(new ReloadSchedulesRequest());
    return NoContent();
  }

  [HttpPost("validate")]
  [Authorize(Roles = "Administrator")]
  public ActionResult Validate([FromBody] Schedule schedule) {
    try {
      if (string.IsNullOrEmpty(schedule.cron)) {
        return Ok(
          new {
            Valid = true,
            NextOccurrences = Array.Empty<DateTime>()
          }
        );
      }

      CronExpression expression =
        CronExpression.Parse(schedule.cron, CronFormat.IncludeSeconds);
      IEnumerable<DateTime> next_occurrences =
        expression.GetOccurrences(
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddYears(1),
                    false,
                    false
                  )
                  .Take(5);

      return Ok(
        new {
          Valid = true,
          NextOccurrences = next_occurrences
        }
      );
    } catch (Exception ex) {
      return BadRequest(
        new {
          Valid = false,
          Error = ex.Message
        }
      );
    }
  }
}