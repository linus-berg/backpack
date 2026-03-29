// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.Jetbrains.IDE;

/// <summary>
/// Consumer for JetBrains IDE artifact processing requests.
/// </summary>
public class Consumer : IProcessor {
  private readonly IJetbrains jetbrains_;

  /// <summary>
  /// Initializes a new instance of the <see cref="Consumer"/> class.
  /// </summary>
  /// <param name="jetbrains">The JetBrains IDE processor.</param>
  public Consumer(IJetbrains jetbrains) {
    jetbrains_ = jetbrains;
  }

  /// <summary>
  /// Consumes the artifact process request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await jetbrains_.ProcessArtifact(artifact);
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
      await jetbrains_.ProcessArtifact(artifact);
      await context.RespondAsync(new ArtifactPreviewResponse { artifact = artifact });
    } catch (Exception e) {
      await context.RespondAsync(new ArtifactPreviewResponse { error = e.Message });
    }
  }
}