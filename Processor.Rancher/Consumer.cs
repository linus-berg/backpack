using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.Rancher;

public class Consumer : IProcessor {
  private readonly IRancher rancher_;

  public Consumer(IRancher rancher) {
    rancher_ = rancher;
  }

  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await rancher_.ProcessArtifact(artifact);
    await context.ProcessorReply(artifact);
  }

  public async Task Consume(ConsumeContext<ArtifactPreviewRequest> context) {
    Artifact artifact = new() {
      id = context.Message.id,
      processor = context.Message.processor
    };
    try {
      await rancher_.ProcessArtifact(artifact);
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