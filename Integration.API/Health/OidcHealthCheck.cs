using Core.Kernel;
using Core.Kernel.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Integration.API.Health;

/// <summary>
///   Fetches the OIDC provider's discovery document
///   (<c>{BP_OIDC_AUTHORITY}/.well-known/openid-configuration</c>). Registered
///   as degraded-only: without the provider no one can sign in, but the API
///   still serves API keys and already validated tokens, so it stays ready.
///   The result is cached for 30 seconds so probes don't hammer the provider.
/// </summary>
public sealed class OidcHealthCheck : IHealthCheck {
  private static readonly TimeSpan S_CACHE_ = TimeSpan.FromSeconds(30);
  private static readonly object S_LOCK_ = new();
  private static (DateTime at, HealthCheckResult result)? s_last_;

  private readonly IHttpClientFactory http_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="OidcHealthCheck" /> class.
  /// </summary>
  /// <param name="http">Creates the HTTP client.</param>
  public OidcHealthCheck(IHttpClientFactory http) {
    http_ = http;
  }

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken token = default) {
    lock (S_LOCK_) {
      if (s_last_ is { } last && DateTime.UtcNow - last.at < S_CACHE_) {
        return last.result;
      }
    }

    HealthCheckResult result;
    string authority =
      Configuration.GetBackpackVariable(CoreVariables.BP_OIDC_AUTHORITY)
                   .TrimEnd('/');
    try {
      using HttpResponseMessage response = await http_
                                                 .CreateClient("health")
                                                 .GetAsync(
                                                   $"{authority}/.well-known/openid-configuration",
                                                   token
                                                 );
      result = response.IsSuccessStatusCode
                 ? HealthCheckResult.Healthy("The OIDC provider answers.")
                 : HealthResults.Failed(
                   context,
                   $"The OIDC provider answered {(int)response.StatusCode}."
                 );
    } catch (Exception e) {
      result = HealthResults.Failed(
        context,
        "The OIDC provider is unreachable.",
        e
      );
    }

    lock (S_LOCK_) {
      s_last_ = (DateTime.UtcNow, result);
    }

    return result;
  }
}
