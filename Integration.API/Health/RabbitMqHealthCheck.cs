using Core.Kernel;
using Core.Kernel.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Integration.API.Health;

/// <summary>
///   Opens and closes an AMQP connection to BP_RABBIT_MQ_HOST, the way
///   MassTransit connects (vhost <c>/</c>, port 5672). MassTransit's own bus
///   check can't tell here: the API consumes no queues, so its bus stays
///   "healthy" while RabbitMQ is down. Degraded-only: the API still serves
///   reads without RabbitMQ.
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck {
  /// <inheritdoc />
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken token = default) {
    ConnectionFactory factory = new() {
      HostName = Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_HOST),
      UserName = Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER),
      Password = Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS),
      VirtualHost = "/",
      ClientProvidedName = "backpack-api-health",
      RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
      AutomaticRecoveryEnabled = false,
      TopologyRecoveryEnabled = false
    };
    try {
      await using IConnection connection =
        await factory.CreateConnectionAsync(token);
      await connection.CloseAsync(token);
      return HealthCheckResult.Healthy("RabbitMQ accepts connections.");
    } catch (BrokerUnreachableException e)
      when (e.InnerException is AuthenticationFailureException) {
      return HealthResults.Failed(
        context,
        "RabbitMQ rejects the credentials.",
        e
      );
    } catch (Exception e) {
      return HealthResults.Failed(
        context,
        "RabbitMQ is unreachable.",
        e
      );
    }
  }
}
