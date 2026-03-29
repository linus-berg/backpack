using Core.Gateway.Consumers;
using Core.Kernel;

namespace Core.Gateway.Definitions;

public class IngestDefinition : BaseGatewayDefinition<IngestConsumer> {
  public IngestDefinition() : base(Endpoints.S_GATEWAY_INGEST_UNPROCESSED) {
  }
}