using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.Php;

public class Consumer : IProcessor {
  private readonly IPhp php_;

  public Consumer(IPhp php) {
    php_ = php;
  }

  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await php_.ProcessArtifact(artifact);
    await context.ProcessorReply(artifact);
  }

  /// <summary>
  ///   Consumes the artifact preview request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Consume(ConsumeContext<ArtifactPreviewRequest> context) {
    try {
      Artifact artifact = new() {
        id = context.Message.id,
        processor = context.Message.processor
      };
      await php_.ProcessArtifact(artifact);
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