using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Administrator")]
public class ApiKeyController : ControllerBase {
  private readonly ICoreDatabase database_;
  private readonly IEventService event_service_;

  public ApiKeyController(ICoreDatabase database, IEventService event_service) {
    database_ = database;
    event_service_ = event_service;
  }

  [HttpGet]
  public async Task<IEnumerable<ApiKey>> Get() {
    return await database_.GetApiKeys();
  }
  
  [HttpPost]
  public async Task<ActionResult<ApiKey>> Post([FromBody] ApiKey input) {
    // We only take the name from input, generate everything else for security
    ApiKey key = new() {
      name = input.name,
      key = Guid.NewGuid().ToString().Replace("-", ""),
      created_by = HttpContext.User.Identity?.Name ?? "Unknown"
    };

    await database_.AddApiKey(key);
    await event_service_.LogEvent(
      "API",
      $"API Key created: {key.name}",
      EventSeverity.SUCCESS,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    return Ok(key);
  }

  [HttpDelete("{id}")]
  public async Task<ActionResult> Delete(string id) {
    bool result = await database_.DeleteApiKey(id);
    if (result) {
      await event_service_.LogEvent(
        "API",
        $"API Key deleted: {id}",
        EventSeverity.WARNING,
        HttpContext.User.Identity?.Name ?? "Unknown"
      );
    }
    return result ? Ok() : BadRequest();
  }
}
