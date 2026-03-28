using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using Integration.API.Output;
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
  public async Task<IEnumerable<ApiKeyOutput>> Get() {
    IEnumerable<ApiKey> keys = await database_.GetApiKeys();
    return keys.Select(k => new ApiKeyOutput {
      id = k.id,
      name = k.name,
      key_preview = $"{k.key.Substring(0, 4)}...{k.key.Substring(k.key.Length - 4)}",
      created_at = k.created_at,
      created_by = k.created_by
    });
  }

  [HttpPost]
  public async Task<ActionResult<object>> Post([FromBody] ApiKey input) {
    // We only take the name from input, generate everything else for security
    string fullKey = Guid.NewGuid().ToString().Replace("-", "");
    ApiKey key = new() {
      name = input.name,
      key = fullKey,
      created_by = HttpContext.User.Identity?.Name ?? "Unknown"
    };

    await database_.AddApiKey(key);
    await event_service_.LogEvent(
      "API",
      $"API Key created: {key.name}",
      EventSeverity.SUCCESS,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    
    // Return BOTH the full key (for initial display) and the output metadata
    return Ok(new {
      id = key.id,
      name = key.name,
      key = fullKey, // Only time the full key is returned
      key_preview = $"{fullKey.Substring(0, 4)}...{fullKey.Substring(fullKey.Length - 4)}",
      created_at = key.created_at,
      created_by = key.created_by
    });
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
