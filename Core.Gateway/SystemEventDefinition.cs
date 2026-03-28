using Core.Kernel;
using MassTransit;

namespace Core.Gateway;

public class SystemEventDefinition : ConsumerDefinition<SystemEventConsumer> {
  public SystemEventDefinition() {
    EndpointName = Endpoints.S_SYSTEM_EVENT.ToString().Replace("queue:", "");
    ConcurrentMessageLimit = 10;
  }

  protected override void ConfigureConsumer(
    IReceiveEndpointConfigurator endpoint_configurator,
    IConsumerConfigurator<SystemEventConsumer> consumer_configurator) {
    endpoint_configurator.UseMessageRetry(
      r => r.Intervals(100, 200, 500, 800, 1000)
    );
    endpoint_configurator.UseInMemoryOutbox();
  }
}
