using Core.Kernel.Messages;
using MassTransit;

namespace Core.Gateway.Consumers;

public class ProcessedConsumer : IConsumer<ArtifactProcessedRequest> {
  private readonly IGatewayProcessingService processing_service_;

  public ProcessedConsumer(IGatewayProcessingService processing_service) {
    processing_service_ = processing_service;
  }

  public async Task Consume(ConsumeContext<ArtifactProcessedRequest> context) {
    await processing_service_.ProcessArtifact(context);
  }
}