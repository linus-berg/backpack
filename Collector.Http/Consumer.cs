// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Messages;
using MassTransit;

namespace Collector.Http;

/// <summary>
///   Consumer for HTTP artifact collection requests.
/// </summary>
public class Consumer : ICollector {
  private readonly bool delta_;
  private readonly bool forward_;
  private readonly FileSystem fs_;
  private readonly IHttpClientFactory http_client_factory_;
  private readonly ILogger<Consumer> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="fs">The file system.</param>
  /// <param name="http_client_factory">The HTTP client factory.</param>
  /// <param name="logger">The logger.</param>
  public Consumer(FileSystem fs, IHttpClientFactory http_client_factory,
                  ILogger<Consumer> logger) {
    fs_ = fs;
    http_client_factory_ = http_client_factory;
    logger_ = logger;
    delta_ =
      Configuration.GetBackpackVariable(
        CoreVariables.BP_COLLECTOR_HTTP_DELTA
      ) ==
      "true";
    forward_ =
      Configuration.GetBackpackVariable(CoreVariables.BP_COLLECTOR_HTTP_MODE) ==
      "forward";
  }

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactCollectRequest> context) {
    string location = context.Message.location;
    string module = context.Message.module;
    string fp = fs_.GetArtifactPath(module, location);
    bool exists = await fs_.Exists(fp);
    if (!exists || context.Message.force) {
      using HttpClient client =
        http_client_factory_.CreateClient("fetch-client");
      RemoteFile rf = new(client, location, fs_);
      if (await rf.Get(fp, context.CancellationToken) && delta_) {
        /* The storage pipeline retries the upload, so the artifact should be in
           place by now. It is confirmed anyway, because a link to a key that is
           not in the bucket is worse than no link at all: a concurrent collect
           of the same key can still have cleared it on its way out. */
        if (!await fs_.Exists(fp)) {
          logger_.LogError(
            "Collected {Location} but {Path} is not in the bucket; not linking",
            location,
            fp
          );
        } else if (!await fs_.CreateDeltaLink(module, location, fp)) {
          logger_.LogError(
            "Collected {Location} but failed to record the delta link for {Path}",
            location,
            fp
          );
        }
      }
    }
  }
}
