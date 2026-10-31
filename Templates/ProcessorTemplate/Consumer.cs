using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor._NAME_;

public class Consumer : IProcessor {
  private readonly I_NAME_ logic_;
  private readonly IEventService events_;

  public Consumer(I_NAME_ logic, IEventService events) {
    logic_ = logic;
    events_ = events;
  }

  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await logic_.ProcessArtifact(artifact);
    await context.ProcessorReply(artifact);
  }

  /* Answers the GUI's preview (queue processor-_ENDPOINT_-preview): the same
     processing, on an artifact that isn't stored, returned to the requester. */
  public async Task Consume(ConsumeContext<ArtifactPreviewRequest> context) {
    Artifact artifact = new() {
      id = context.Message.id,
      processor = context.Message.processor,
      filter = string.Empty,
      config = context.Message.config
    };
    try {
      await logic_.ProcessArtifact(artifact);
      await context.RespondAsync(
        new ArtifactPreviewResponse {
          artifact = artifact
        }
      );
    } catch (Exception e) {
      await context.RespondAsync(
        new ArtifactPreviewResponse {
          error = e.Message
        }
      );
    }
  }
}
