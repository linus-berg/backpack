using Core.Gateway.Consumers;
using Core.Kernel;

namespace Core.Gateway.Definitions;

public class ProcessedDefinition : BaseGatewayDefinition<ProcessedConsumer> {
  public ProcessedDefinition() : base(Endpoints.S_GATEWAY_INGEST_PROCESSED) {
  }
}
