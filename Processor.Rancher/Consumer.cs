using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Processor.Rancher;

public class Consumer {
  private readonly IRancher rancher_;

  public Consumer(IRancher rancher) {
    rancher_ = rancher;
  }

  public async Task Handle(ArtifactProcessRequest request, IMessageContext context) {
    Artifact artifact = request.artifact;
    await rancher_.ProcessArtifact(artifact);
    await context.ProcessorReply(request, artifact);
  }

  public async Task<ArtifactPreviewResponse> Handle(ArtifactPreviewRequest request) {
    Artifact artifact = new() {
      id = request.id,
      processor = request.processor,
      filter = string.Empty
    };
    try {
      await rancher_.ProcessArtifact(artifact);
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