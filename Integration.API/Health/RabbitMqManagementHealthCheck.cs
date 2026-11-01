using System.Net.Http.Headers;
using System.Text;
using Core.Kernel;
using Core.Kernel.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Integration.API.Health;

/// <summary>
///   Checks the RabbitMQ management API (BP_RABBIT_MQ_API), which the Status
///   page and queue purging use. Degraded-only: messaging itself is covered
///   by MassTransit's RabbitMQ check.
/// </summary>
public sealed class RabbitMqManagementHealthCheck : IHealthCheck {
  private readonly IHttpClientFactory http_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RabbitMqManagementHealthCheck" /> class.
  /// </summary>
  /// <param name="http">Creates the HTTP client.</param>
  public RabbitMqManagementHealthCheck(IHttpClientFactory http) {
    http_ = http;
  }

  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken token = default) {
    try {
      string api = Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_API)
                                .TrimEnd('/');
      string credentials = Convert.ToBase64String(
        Encoding.UTF8.GetBytes(
          $"{Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER)}:{Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS)}"
        )
      );
      using HttpRequestMessage request = new(HttpMethod.Get, $"{api}/api/overview");
      request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
      using HttpResponseMessage response =
        await http_.CreateClient("health").SendAsync(request, token);
      return response.IsSuccessStatusCode
               ? HealthCheckResult.Healthy("The RabbitMQ management API answers.")
               : HealthResults.Failed(
                 context,
                 $"The RabbitMQ management API answered {(int)response.StatusCode}."
               );
    } catch (Exception e) {
      return HealthResults.Failed(
        context,
        "The RabbitMQ management API is unreachable.",
        e
      );
    }
  }
}
