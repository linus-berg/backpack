using System.Security.Claims;
using Integration.API.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
      authority = Environment.GetEnvironmentVariable("OIDC_AUTHORITY") ??
                  "http://localhost:8090/realms/backpack",
      client_id = Environment.GetEnvironmentVariable("OIDC_AUDIENCE") ??
                  "backpack"
    };
  }
}