using Core.Gateway.Consumers;
using MassTransit;

namespace Core.Gateway.Definitions;

public abstract class BaseGatewayDefinition<T> : ConsumerDefinition<T> where T : class, IConsumer {
  protected BaseGatewayDefinition(Uri endpoint_uri) {
    EndpointName = endpoint_uri.ToString().Replace("queue:", "");
    ConcurrentMessageLimit = 10;
  }

  protected override void ConfigureConsumer(
    IReceiveEndpointConfigurator endpoint_configurator,
    IConsumerConfigurator<T> consumer_configurator) {
    endpoint_configurator.UseMessageRetry(
      r => r.Intervals(100, 200, 500, 800, 1000)
    );
    endpoint_configurator.UseInMemoryOutbox();
  }
}
