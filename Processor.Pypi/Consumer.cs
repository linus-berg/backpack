// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Extensions;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Processor.Pypi;

/// <summary>
/// Consumer for PyPI artifact processing requests.
/// </summary>
public class Consumer : IProcessor {
  private readonly IPypi pypi_;

  /// <summary>
  /// Initializes a new instance of the <see cref="Consumer"/> class.
  /// </summary>
  /// <param name="pypi">The PyPI processor.</param>
  public Consumer(IPypi pypi) {
    pypi_ = pypi;
  }

  /// <summary>
  /// Consumes the artifact process request.
  /// </summary>
  /// <param name="context">The consume context.</param>
  /// <returns>A task that represents the consume operation.</returns>
  public async Task Consume(ConsumeContext<ArtifactProcessRequest> context) {
    Artifact artifact = context.Message.artifact;
    await pypi_.ProcessArtifact(artifact);
    await context.ProcessorReply(artifact);
  }
}
