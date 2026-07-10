// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using Wolverine;

namespace Collector.Wget;

/// <summary>
///   Consumer for wget artifact collection requests.
/// </summary>
public class Consumer {
  private readonly Wget wget_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="wget">The wget collector.</param>
  public Consumer(Wget wget) {
    wget_ = wget;
  }

  /// <inheritdoc />
  public async Task Handle(ArtifactCollectRequest request, IMessageContext context, CancellationToken cancellationToken) {
    string location = request.location;
    string module = request.module;
    await wget_.Mirror(location);
  }
}