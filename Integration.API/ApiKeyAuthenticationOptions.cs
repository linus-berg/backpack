using Microsoft.AspNetCore.Authentication;

namespace Integration.API;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions {
  public const string C_SCHEME = "ApiKey";
}