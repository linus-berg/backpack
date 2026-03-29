using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.OperatorHub;

public class Consumer : IProcessor {
  private readonly IOperatorHub operator_hub_;

  public Consumer(IOperatorHub operator_hub) {
    operator_hub_ = operator_hub;
  }

  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await operator_hub_.ProcessArtifact(artifact);
    await context.ProcessorReply(artifact);
  }

  /// <summary>
  /// Consumes the artifact preview request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Consume(ConsumeContext<ArtifactPreviewRequest> context) {
    try {
      Artifact artifact = new() {
        id = context.Message.id,
        processor = context.Message.processor
      };
      await operator_hub_.ProcessArtifact(artifact);
      await context.RespondAsync(new ArtifactPreviewResponse { artifact = artifact });
    } catch (Exception e) {
      await context.RespondAsync(new ArtifactPreviewResponse { error = e.Message });
    }
  }
}