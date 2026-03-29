// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using MassTransit;

namespace Collector.Wget;

/// <summary>
///   Consumer for wget artifact collection requests.
/// </summary>
public class Consumer : ICollector {
  private readonly Wget wget_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="wget">The wget collector.</param>
  public Consumer(Wget wget) {
    wget_ = wget;
  }

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactCollectRequest> context) {
    string location = context.Message.location;
    string module = context.Message.module;
    await wget_.Mirror(location);
  }
}