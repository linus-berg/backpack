// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using Wolverine;

namespace Collector.Git;

/// <summary>
///   Consumer for git artifact collection requests.
/// </summary>
public class Consumer {
  private readonly Git git_;
  private readonly ILogger<Consumer> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="git">The git collector.</param>
  /// <param name="logger">The logger.</param>
  public Consumer(Git git, ILogger<Consumer> logger) {
    git_ = git;
    logger_ = logger;
  }

  /// <inheritdoc />
  public async Task Handle(ArtifactCollectRequest request, IMessageContext context, CancellationToken cancellationToken) {
    string location = request.location;
    string module = request.module;
    try {
      await git_.Mirror(location, cancellationToken);
    } catch (Exception e) {
      logger_.LogError(
        "{Location} failed with error {Error}",
        location,
        e.ToString()
      );
      throw;
    }
  }
}