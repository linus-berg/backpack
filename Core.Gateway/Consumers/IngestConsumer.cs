using Core.Kernel.Messages;
using Core.Services;
using Wolverine;

namespace Core.Gateway.Consumers;

public class IngestConsumer {
  private readonly IArtifactService aps_;
  private readonly IMessageBus bus_;
  private readonly ICoreCache cache_;
  private readonly ILogger<IngestConsumer> logger_;

  public IngestConsumer(ILogger<IngestConsumer> logger, IMessageBus bus,
                        ICoreCache cache,
                        IArtifactService aps) {
    logger_ = logger;
    bus_ = bus;
    cache_ = cache;
    aps_ = aps;
  }

  public async Task Handle(ArtifactIngestRequest request, IMessageContext context) {
    /* Run as init */
    await aps_.Process(request.artifact);
    logger_.LogInformation(
      "INGESTED:{ArtifactProcessor}:{ArtifactId}",
      request.artifact.processor,
      request.artifact.id
    );
  }
}