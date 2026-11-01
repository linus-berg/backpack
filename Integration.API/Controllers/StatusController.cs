using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using Integration.API.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase {
  private readonly ICoreDatabase database_;
  private readonly IEventService event_service_;
  private readonly InfrastructureHealthService infrastructure_health_;
  private readonly IStatusService status_service_;

  public StatusController(IStatusService status_service, ICoreDatabase database,
                          IEventService event_service,
                          InfrastructureHealthService infrastructure_health) {
    status_service_ = status_service;
    database_ = database;
    event_service_ = event_service;
    infrastructure_health_ = infrastructure_health;
  }

  [HttpGet("status")]
  public ActionResult GetStatus() {
    return Ok("Backpack is OK.");
  }

  /// <summary>
  ///   Health of RabbitMQ, its management API, MongoDB, Redis, S3 (when the
  ///   API has the S3 settings) and the OIDC provider, refreshed at most
  ///   every 10 seconds.
  /// </summary>
  [HttpGet("health")]
  [Authorize]
  public async Task<InfrastructureHealth> GetInfrastructureHealth(
    CancellationToken token) {
    return await infrastructure_health_.GetAsync(token);
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