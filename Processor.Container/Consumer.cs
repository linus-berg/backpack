// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Library.Skopeo;
using MassTransit;

namespace Processor.Container;

/// <summary>
///   Consumer for container artifact processing requests.
/// </summary>
public class Consumer : IProcessor {
  private readonly SkopeoClient skopeo_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="skopeo">The Skopeo client.</param>
  public Consumer(SkopeoClient skopeo) {
    skopeo_ = skopeo;
  }

  /// <summary>
  ///   Consumes the artifact process request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    ArtifactProcessRequest request = context.Message;
    Artifact artifact = request.artifact;
    await GetTags(artifact);
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
        processor = context.Message.processor,
        filter = string.Empty
      };
      await GetTags(artifact);
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

  private async Task GetTags(Artifact artifact) {
    SkopeoListTagsOutput? list_tags = await skopeo_.GetTags(artifact.id);
    if (list_tags?.tags != null) {
      foreach (string tag in list_tags.tags) {
        if (artifact.HasVersion(tag)) {
          continue;
        }

        ArtifactVersion version = new() {
          version = tag
        };
        version.AddFile(
          $"{artifact.id}:{tag}",
          $"docker://{list_tags.repository}:{tag}"
        );
        artifact.AddVersion(version);
      }
    }
  }
}