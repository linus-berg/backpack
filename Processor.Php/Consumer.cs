using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Processor.Php;

public class Consumer {
  private readonly IPhp php_;

  public Consumer(IPhp php) {
    php_ = php;
  }

  public async Task Handle(ArtifactProcessRequest request, IMessageContext context) {
    Artifact artifact = request.artifact;
    await php_.ProcessArtifact(artifact);
    await context.ProcessorReply(request, artifact);
  }

  /// <summary>
  ///   Consumes the artifact preview request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task<ArtifactPreviewResponse> Handle(ArtifactPreviewRequest request) {
    try {
      Artifact artifact = new() {
        id = request.id,
        processor = request.processor,
        filter = string.Empty
      };
      await php_.ProcessArtifact(artifact);
      return new ArtifactPreviewResponse {
          artifact = artifact
        };
    } catch (Exception e) {
      return new ArtifactPreviewResponse {
          error = e.Message
        };
    }
  }
}