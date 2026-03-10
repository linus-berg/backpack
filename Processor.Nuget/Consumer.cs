// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.Nuget;

/// <summary>
/// Consumer for NuGet artifact processing requests.
/// </summary>
public class Consumer : IProcessor {
  private readonly INuget nuget_;

  /// <summary>
  /// Initializes a new instance of the <see cref="Consumer"/> class.
  /// </summary>
  /// <param name="nuget">The NuGet processor.</param>
  public Consumer(INuget nuget) {
    nuget_ = nuget;
  }

  /// <summary>
  /// Consumes the artifact process request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await nuget_.ProcessArtifact(artifact);
    await context.ProcessorReply(artifact);
  }
}