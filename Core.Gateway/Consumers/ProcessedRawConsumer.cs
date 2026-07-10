using Core.Kernel.Messages;
using Wolverine;

namespace Core.Gateway.Consumers;

public class ProcessedRawConsumer {
  private readonly IGatewayProcessingService processing_service_;

  public ProcessedRawConsumer(IGatewayProcessingService processing_service) {
    processing_service_ = processing_service;
  }

  public async Task Handle(ArtifactProcessedRequest request, IMessageContext context) {
    await processing_service_.ProcessArtifact(request);
  }
}