// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Messages;
using Wolverine;

namespace Collector.Http;

/// <summary>
///   Consumer for HTTP artifact collection requests.
/// </summary>
public class Consumer {
  private readonly bool delta_;
  private readonly bool forward_;
  private readonly FileSystem fs_;
  private readonly IHttpClientFactory http_client_factory_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="fs">The file system.</param>
  /// <param name="http_client_factory">The HTTP client factory.</param>
  public Consumer(FileSystem fs, IHttpClientFactory http_client_factory) {
    fs_ = fs;
    http_client_factory_ = http_client_factory;
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
  public async Task Handle(ArtifactCollectRequest request, IMessageContext context, CancellationToken cancellationToken) {
    string location = request.location;
    string module = request.module;
    string fp = fs_.GetArtifactPath(module, location);
    bool exists = await fs_.Exists(fp);
    if (!exists || request.force) {
      using HttpClient client =
        http_client_factory_.CreateClient("fetch-client");
      RemoteFile rf = new(client, location, fs_);
      if (await rf.Get(fp, cancellationToken)) {
        if (delta_) {
          await fs_.CreateDeltaLink(module, location);
        }
      }
    }
  }
}
