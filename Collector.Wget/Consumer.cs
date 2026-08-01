// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Messages;
using MassTransit;

namespace Collector.Wget;

/// <summary>
///   Consumer for wget artifact collection requests. Uses the native C# WebMirror
///   engine to recursively mirror websites and upload them to S3 storage.
/// </summary>
public class Consumer : ICollector {
  private readonly ILogger<Consumer> logger_;
  private readonly WebMirror web_mirror_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="web_mirror">The native web mirroring engine.</param>
  /// <param name="logger">The logger.</param>
  public Consumer(WebMirror web_mirror, ILogger<Consumer> logger) {
    web_mirror_ = web_mirror;
    logger_ = logger;
  }

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactCollectRequest> context) {
    string location = context.Message.location;
    string module = context.Message.module;
    logger_.LogInformation(
      "Starting wget mirror for module '{Module}' at '{Location}'",
      module,
      location
    );

    bool success = await web_mirror_.Mirror(location,
      context.CancellationToken);

    if (success) {
      logger_.LogInformation(
        "Successfully mirrored '{Location}' for module '{Module}'",
        location,
        module
      );
    } else {
      logger_.LogWarning(
        "Mirror operation failed for '{Location}' in module '{Module}'",
        location,
        module
      );
    }
  }
}