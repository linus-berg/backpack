// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Processor.Npm;

/// <summary>
///   Consumer for NPM artifact processing and preview requests.
/// </summary>
public class Consumer {
  private readonly INpm npm_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="npm">The NPM processor.</param>
  public Consumer(INpm npm) {
    npm_ = npm;
  }

  /// <summary>
  ///   Consumes the artifact process request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Handle(ArtifactProcessRequest request, IMessageContext context) {
    Artifact artifact = request.artifact;
    await npm_.ProcessArtifact(artifact);
    await context.ProcessorReply(request, artifact);
  }

  /// <summary>
  ///   Consumes the artifact preview request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task<ArtifactPreviewResponse> Handle(ArtifactPreviewRequest request) {
    Artifact artifact = new() {
      id = request.id,
      processor = request.processor,
      filter = string.Empty
    };
    try {
      await npm_.ProcessArtifact(artifact);
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