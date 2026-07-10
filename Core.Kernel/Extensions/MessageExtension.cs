// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Core.Kernel.Extensions;

/// <summary>
///   Provides extension methods for <see cref="IMessageContext" /> to simplify message sending and processing.
/// </summary>
public static class MessageExtension {
  /// <summary>
  ///   Sends a collection request for an artifact at a specific location.
  /// </summary>
  /// <param name="ctx">The message context.</param>
  /// <param name="location">The location of the artifact.</param>
  /// <param name="processor">The name of the processor module.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task Collect(this IMessageContext ctx, string location,
                                   string processor) {
    ArtifactCollectRequest request = new() {
      location = location,
      module = processor
    };
    await ctx.EndpointFor(new Uri($"rabbitmq://queue/{request.GetCollectorModule()}"))
             .SendAsync(request);
  }

  /// <summary>
  ///   Sends a reply to the gateway indicating that an artifact has been processed.
  /// </summary>
  /// <param name="context">The message context for the process request.</param>
  /// <param name="request">The original request containing the context ID.</param>
  /// <param name="artifact">The processed artifact.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  public static async Task ProcessorReply(
    this IMessageContext context,
    ArtifactProcessRequest request,
    Artifact artifact) {
    await context.EndpointFor(Endpoints.S_GATEWAY_INGEST_PROCESSED)
                 .SendAsync(new ArtifactProcessedRequest {
                   context = request.ctx,
                   artifact = artifact
                 });
  }
}