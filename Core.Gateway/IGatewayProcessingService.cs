using Core.Kernel.Messages;

namespace Core.Gateway;

public interface IGatewayProcessingService {
  Task ProcessArtifact(ArtifactProcessedRequest request);
}