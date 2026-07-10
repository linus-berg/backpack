using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Processor.Terraform;

public class Consumer {
  private readonly ITerraform terraform_;

  public Consumer(ITerraform terraform) {
    terraform_ = terraform;
  }

  public async Task Handle(ArtifactProcessRequest request, IMessageContext context) {
    Artifact artifact = request.artifact;
    await terraform_.ProcessArtifact(artifact);
    await context.ProcessorReply(request, artifact);
  }

  public async Task<ArtifactPreviewResponse> Handle(ArtifactPreviewRequest request) {
    Artifact artifact = new() {
      id = request.id,
      processor = request.processor,
      filter = string.Empty
    };
    try {
      await terraform_.ProcessArtifact(artifact);
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