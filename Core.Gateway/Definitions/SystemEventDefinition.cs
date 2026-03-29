using Core.Gateway.Consumers;
using Core.Kernel;

namespace Core.Gateway.Definitions;

public class
  SystemEventDefinition : BaseGatewayDefinition<SystemEventConsumer> {
  public SystemEventDefinition() : base(Endpoints.S_SYSTEM_EVENT) {
  }
}