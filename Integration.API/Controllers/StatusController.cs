using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase {
  private readonly ICoreDatabase database_;
  private readonly IEventService event_service_;
  private readonly IStatusService status_service_;

  public StatusController(IStatusService status_service, ICoreDatabase database,
                          IEventService event_service) {
    status_service_ = status_service;
    database_ = database;
    event_service_ = event_service;
  }

  [HttpGet("status")]
  public ActionResult GetStatus() {
    return Ok("Backpack is OK.");
  }

  [HttpGet("queue")]
  public async Task<List<QueueStatus>> GetQueueStatus() {
    return await status_service_.QueueStatus();
  }

  [HttpDelete("queue/{queue_name}")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> PurgeQueue(string queue_name) {
    bool result = await status_service_.PurgeQueue(queue_name);
    if (result) {
      await event_service_.LogEvent(
        "API",
        $"Queue {queue_name} was purged",
        EventSeverity.WARNING,
        HttpContext.User.Identity?.Name ?? "Unknown"
      );
    }

    return result ? Ok() : BadRequest();
  }
}