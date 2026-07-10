// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Processor.Helm;

/// <summary>
///   Consumer for Helm chart artifact processing requests.
/// </summary>
public class Consumer {
  private readonly Helm helm_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="helm">The Helm processor.</param>
  public Consumer(Helm helm) {
    helm_ = helm;
  }

  /// <summary>
  ///   Consumes the artifact process request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Handle(ArtifactProcessRequest request, IMessageContext context) {
    Artifact artifact = request.artifact;
    await helm_.ProcessArtifact(artifact);
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
      await helm_.ProcessArtifact(artifact);
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