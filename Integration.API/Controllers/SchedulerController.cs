using Core.Kernel;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Core.Services;
using Cronos;
using Integration.API.Jobs;
using MassTransit;
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
  private readonly JobStarter jobs_;
  private readonly IPublishEndpoint publish_endpoint_;

  public SchedulerController(ICoreDatabase database, IArtifactService aps,
                             IEventService event_service,
                             IPublishEndpoint publish_endpoint,
                             JobStarter jobs) {
    database_ = database;
    aps_ = aps;
    event_service_ = event_service;
    publish_endpoint_ = publish_endpoint;
    jobs_ = jobs;
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
    /* A background re-track job: 202 with the job, or 409 while another
       job runs. */
    return await jobs_.Start(
             JobKinds.TRACK,
             processor,
             HttpContext.User.Identity?.Name ?? "Unknown"
           );
  }

  [HttpPut]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Update([FromBody] Schedule schedule) {
    if (string.IsNullOrEmpty(schedule.processor) ||
        string.IsNullOrEmpty(schedule.cron)) {
      return BadRequest("Processor and Cron are required");
    }

    string? cron_error = GetCronError(schedule.cron);
    if (cron_error != null) {
      return BadRequest(
        new {
          Valid = false,
          Error = cron_error
        }
      );
    }

    await database_.UpdateSchedule(schedule);
    await event_service_.LogEvent(
      "API",
      $"Schedule updated for {schedule.processor}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    await publish_endpoint_.Publish(new ReloadSchedulesRequest());
    return Ok(schedule);
  }

  [HttpPost]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Add([FromBody] Schedule schedule) {
    if (string.IsNullOrEmpty(schedule.processor) ||
        string.IsNullOrEmpty(schedule.cron)) {
      return BadRequest("Processor and Cron are required");
    }

    string? cron_error = GetCronError(schedule.cron);
    if (cron_error != null) {
      return BadRequest(
        new {
          Valid = false,
          Error = cron_error
        }
      );
    }

    schedule.id ??= Guid.NewGuid().ToString();
    await database_.AddSchedule(schedule);
    await event_service_.LogEvent(
      "API",
      $"Schedule added for {schedule.processor}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    await publish_endpoint_.Publish(new ReloadSchedulesRequest());
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
    await publish_endpoint_.Publish(new ReloadSchedulesRequest());
    return NoContent();
  }

  /// <summary>
  ///   Parses a six-field (seconds-first) cron expression.
  /// </summary>
  /// <returns>The parse error message, or null when the expression is valid.</returns>
  private static string? GetCronError(string cron) {
    try {
      CronExpression.Parse(cron, CronFormat.IncludeSeconds);
      return null;
    } catch (Exception ex) {
      return ex.Message;
    }
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