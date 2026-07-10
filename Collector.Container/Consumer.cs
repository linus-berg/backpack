// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using Library.Skopeo;
using Wolverine;

namespace Collector.Container;

/// <summary>
///   Consumer for artifact collection requests.
/// </summary>
public class Consumer {
  private readonly SkopeoClient skopeo_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="skopeo">The Skopeo client.</param>
  public Consumer(SkopeoClient skopeo) {
    skopeo_ = skopeo;
  }

  /// <inheritdoc />
  public async Task Handle(ArtifactCollectRequest request, IMessageContext context, CancellationToken cancellationToken) {
    
    /* Collect if missing manifest or layers */

    SkopeoManifest? manifest = await skopeo_.ImageExists(request.location);
    if (manifest != null) {
      return;
    }

    await skopeo_.CopyToRegistry(request.location);
  }
}