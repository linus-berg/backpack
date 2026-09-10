// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Messages;
using MassTransit;

namespace Collector.Huggingface;

/// <summary>
///   Consumer for HTTP artifact collection requests.
/// </summary>
public class Consumer : ICollector {
  private readonly bool delta_;
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
  }

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactCollectRequest> context) {
    string location = context.Message.location;
    string module = context.Message.module;
    
    // We parse the URI to build the correct S3 path: <org>/<model>/<filepath>
    // Example: hf://moonshotai/Kimi-K2.7-Code/figures/kimi-logo.png?modelId=moonshotai/Kimi-K2.7-Code
    // Should result in: moonshotai/Kimi-K2.7-Code/figures/kimi-logo.png
    
    Uri uri = new(location);
    
    // The final S3 path: <module>/<modelId>/<filename>
    string fp = Path.Join(module, uri.Host, uri.LocalPath);

    // The HuggingFace collector always performs an ETAG check via RemoteFile.Get.
    // It only downloads the file if the remote ETAG differs from the local one.
    using HttpClient client =
      http_client_factory_.CreateClient("fetch-client");
    RemoteFile rf = new(client, location, fs_);
    if (await rf.Get(fp, context.CancellationToken) && delta_) {
      /* The storage pipeline retries the upload, so the artifact should be in
         place by now. It is confirmed anyway, because a link to a key that is
         not in the bucket is worse than no link at all: a concurrent collect of
         the same key can still have cleared it on its way out.

         fp is also handed to CreateDeltaLink explicitly: it is derived here
         rather than by FileSystem.GetArtifactPath, and deriving the target a
         second time would record a path this collector never wrote to. */
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
