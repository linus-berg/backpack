using Core.Gateway.Consumers;
using Core.Kernel;
using MassTransit;

namespace Core.Gateway.Definitions;

public class
  ProcessedRawDefinition : BaseGatewayDefinition<ProcessedRawConsumer> {
  public ProcessedRawDefinition() : base(
    Endpoints.S_GATEWAY_INGEST_PROCESSED_RAW
  ) {
  }

  [Obsolete("Use the IRegistrationContext overload instead. Visit https://masstransit.io/obsolete for details.")]
  protected override void ConfigureConsumer(
    IReceiveEndpointConfigurator endpoint_configurator,
    IConsumerConfigurator<ProcessedRawConsumer> consumer_configurator) {
    // Call base to get common retry and outbox logic
    base.ConfigureConsumer(endpoint_configurator, consumer_configurator);

    // Specific requirement for the raw ingestion queue
    endpoint_configurator.UseRawJsonDeserializer(isDefault: true);
  }
}