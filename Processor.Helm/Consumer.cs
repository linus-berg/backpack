// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.Helm;

/// <summary>
/// Consumer for Helm chart artifact processing requests.
/// </summary>
public class Consumer : IProcessor {
  private readonly Helm helm_;

  /// <summary>
  /// Initializes a new instance of the <see cref="Consumer"/> class.
  /// </summary>
  /// <param name="helm">The Helm processor.</param>
  public Consumer(Helm helm) {
    helm_ = helm;
  }

  /// <summary>
  /// Consumes the artifact process request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await helm_.ProcessArtifact(artifact);
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
      await helm_.ProcessArtifact(artifact);
      await context.RespondAsync(new ArtifactPreviewResponse { artifact = artifact });
    } catch (Exception e) {
      await context.RespondAsync(new ArtifactPreviewResponse { error = e.Message });
    }
  }
}