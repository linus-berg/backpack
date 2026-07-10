using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Processor._NAME_;

public class Consumer {
  private readonly I_NAME_ logic_;
  private readonly IEventService events_;

  public Consumer(I_NAME_ logic, IEventService events) {
    logic_ = logic;
    events_ = events;
  }

  public async Task Handle(ArtifactProcessRequest request, IMessageContext context) {
    Artifact artifact = request.artifact;
    await logic_.ProcessArtifact(artifact);
    await context.ProcessorReply(request, artifact);
  }
}
