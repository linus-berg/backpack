using System.Security.Claims;
using Core.Kernel;
using Integration.API.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Configuration = Core.Kernel.Configuration;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AuthController : ControllerBase {
  [HttpGet("me")]
  public UserOutput GetUserInfo() {
    return new UserOutput {
      name = User.Identity?.Name ?? "Unknown",
      roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList()
    };
  }

  [HttpGet("oidc")]
  [AllowAnonymous]
  public OidcOptions GetOidcConfig() {
    return new OidcOptions {
      authority =
        Configuration.GetBackpackVariable(CoreVariables.BP_OIDC_AUTHORITY),
      client_id =
        Configuration.GetBackpackVariable(CoreVariables.BP_OIDC_AUDIENCE)
    };
  }
}