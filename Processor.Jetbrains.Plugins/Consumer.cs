// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.Jetbrains.Plugins;

/// <summary>
/// Consumer for JetBrains plugin artifact processing requests.
/// </summary>
public class Consumer : IProcessor {
  private readonly IJetbrains jetbrains_;

  /// <summary>
  /// Initializes a new instance of the <see cref="Consumer"/> class.
  /// </summary>
  /// <param name="jetbrains">The JetBrains plugin processor.</param>
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
}