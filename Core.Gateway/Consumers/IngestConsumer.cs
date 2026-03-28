using Core.Kernel.Messages;
using Core.Services;
using MassTransit;

namespace Core.Gateway.Consumers;

public class IngestConsumer : IConsumer<ArtifactIngestRequest> {
  private readonly IArtifactService aps_;
  private readonly IBus bus_;
  private readonly ICoreCache cache_;
  private readonly ILogger<IngestConsumer> logger_;

  public IngestConsumer(ILogger<IngestConsumer> logger, IBus bus,
                        ICoreCache cache,
                        IArtifactService aps) {
    logger_ = logger;
    bus_ = bus;
    cache_ = cache;
    aps_ = aps;
  }

  public async Task Consume(ConsumeContext<ArtifactIngestRequest> context) {
    /* Run as init */
    ArtifactIngestRequest request = context.Message;
    await aps_.Process(request.artifact);
    logger_.LogInformation(
      "INGESTED:{ArtifactProcessor}:{ArtifactId}",
      request.artifact.processor,
      request.artifact.id
    );
  }
}