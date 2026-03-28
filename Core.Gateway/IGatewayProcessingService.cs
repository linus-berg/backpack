using Core.Kernel.Messages;
using MassTransit;

namespace Core.Gateway;

public interface IGatewayProcessingService {
  Task ProcessArtifact(ConsumeContext<ArtifactProcessedRequest> context);
}
