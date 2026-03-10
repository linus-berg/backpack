// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using MassTransit;

namespace Collector.Rsync;

/// <summary>
///   Consumer for rsync artifact collection requests.
/// </summary>
public class Consumer : ICollector {
  private readonly RSync rsync_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="rsync">The rsync collector.</param>
  public Consumer(RSync rsync) {
    rsync_ = rsync;
  }

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactCollectRequest> context) {
    string location = context.Message.location;
    string module = context.Message.module;
    await rsync_.Mirror(location);
  }
}
