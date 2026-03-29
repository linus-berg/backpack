// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using MassTransit;
using Polly;
using Polly.Registry;

namespace Collector.DockerArchive;

/// <summary>
///   Consumer for docker archive collection requests.
/// </summary>
public class Consumer : ICollector {
  private readonly Docker docker_;
  private readonly ResiliencePipeline<bool> pipeline_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="docker">The docker collector.</param>
  /// <param name="polly">The resilience pipeline provider.</param>
  public Consumer(Docker docker, ResiliencePipelineProvider<string> polly) {
    pipeline_ = polly.GetPipeline<bool>("skopeo-retry");
    docker_ = docker;
  }

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactCollectRequest> context) {
    ArtifactCollectRequest request = context.Message;
    /* Collect if missing manifest or layers */
    await pipeline_.ExecuteAsync(
      async (state, token) =>
        await docker_.GetTarArchive(state.location),
      request,
      context.CancellationToken
    );
  }
}