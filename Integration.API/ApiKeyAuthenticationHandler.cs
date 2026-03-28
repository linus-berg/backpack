using System.Security.Claims;
using System.Text.Encodings.Web;
using Core.Kernel.Models;
using Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Integration.API;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions> {
  private readonly ICoreDatabase database_;
  private const string C_HEADER_NAME_ = "X-API-KEY";

  public ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ICoreDatabase database)
    : base(options, logger, encoder) {
    database_ = database;
  }

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
    if (!Request.Headers.TryGetValue(C_HEADER_NAME_, out var api_key_header_values)) {
      return AuthenticateResult.NoResult();
    }

    string? provided_api_key = api_key_header_values.FirstOrDefault();

    if (string.IsNullOrWhiteSpace(provided_api_key)) {
      return AuthenticateResult.NoResult();
    }

    ApiKey? api_key = await database_.GetApiKey(provided_api_key);

    if (api_key == null) {
      return AuthenticateResult.Fail("Invalid API Key");
    }

    List<Claim> claims = new() {
      new Claim(ClaimTypes.Name, api_key.name),
    };

    ClaimsIdentity identity = new(claims, Scheme.Name);
    ClaimsPrincipal principal = new(identity);
    AuthenticationTicket ticket = new(principal, Scheme.Name);

    return AuthenticateResult.Success(ticket);
  }
}
