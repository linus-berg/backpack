// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using Wolverine;

namespace Collector.Rsync;

/// <summary>
///   Consumer for rsync artifact collection requests.
/// </summary>
public class Consumer {
  private readonly RSync rsync_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="rsync">The rsync collector.</param>
  public Consumer(RSync rsync) {
    rsync_ = rsync;
  }

  /// <inheritdoc />
  public async Task Handle(ArtifactCollectRequest request, IMessageContext context, CancellationToken cancellationToken) {
    string location = request.location;
    string module = request.module;
    await rsync_.Mirror(location);
  }
}