using Core.Kernel.Models;
using Core.Services;
using Integration.API.Output;
using Keycloak.AuthServices.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase {
  private readonly KeycloakAuthenticationOptions kc_opt_ = new();
  private readonly IStatusService status_service_;

  public StatusController(IConfiguration configuration, IStatusService status_service) {
    KeycloakAuthenticationOptions opts = new();
    status_service_ = status_service;
    configuration
      .GetSection(KeycloakAuthenticationOptions.Section)
      .Bind(kc_opt_, opt => opt.BindNonPublicProperties = true);
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
    return result ? Ok() : BadRequest();
  }
  
  [HttpGet("keycloak")]
  public ActionResult GetKeycloak() {
    return Ok(
      new KeycloakOptions {
        url = kc_opt_.AuthServerUrl,
        realm = kc_opt_.Realm,
        resource = kc_opt_.Resource
      }
    );
  }
}