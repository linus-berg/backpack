// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Messages;
using Wolverine;

namespace Collector.Huggingface;

/// <summary>
///   Consumer for HTTP artifact collection requests.
/// </summary>
public class Consumer {
  private readonly bool delta_;
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
  }

  /// <inheritdoc />
  public async Task Handle(ArtifactCollectRequest request, IMessageContext context, CancellationToken cancellationToken) {
    string location = request.location;
    string module = request.module;
    
    // We parse the URI to build the correct S3 path: <org>/<model>/<filepath>
    // Example: hf://moonshotai/Kimi-K2.7-Code/figures/kimi-logo.png?modelId=moonshotai/Kimi-K2.7-Code
    // Should result in: moonshotai/Kimi-K2.7-Code/figures/kimi-logo.png
    
    Uri uri = new(location);
    string? model_id = GetQueryParam(uri, "modelId");
    string filename = GetFilename(uri, model_id ?? "");
    
    // The final S3 path: <module>/<modelId>/<filename>
    string fp = Path.Join(module, model_id ?? "", filename);

    // The HuggingFace collector always performs an ETAG check via RemoteFile.Get.
    // It only downloads the file if the remote ETAG differs from the local one.
    using HttpClient client =
      http_client_factory_.CreateClient("fetch-client");
    RemoteFile rf = new(client, location, fs_);
    if (await rf.Get(fp, cancellationToken)) {
      if (delta_) {
        await fs_.CreateDeltaLink(module, location);
      }
    }
  }

  private string? GetQueryParam(Uri uri, string name) {
    string query = uri.Query;
    if (string.IsNullOrEmpty(query)) return null;

    string pattern = $"{name}=";
    int index = query.IndexOf(pattern, StringComparison.Ordinal);
    if (index < 0) return null;

    string value = query.Substring(index + pattern.Length);
    int ampersand_index = value.IndexOf('&');
    if (ampersand_index >= 0) {
      value = value.Substring(0, ampersand_index);
    }

    return Uri.UnescapeDataString(value);
  }

  private string GetFilename(Uri uri, string modelId) {
    string combined = $"{uri.Host}{uri.LocalPath}";
    if (string.IsNullOrEmpty(modelId)) return Path.GetFileName(uri.LocalPath);
    
    if (combined.StartsWith(modelId, StringComparison.Ordinal)) {
      string filename = combined.Substring(modelId.Length);
      return filename.TrimStart('/');
    }

    return Path.GetFileName(uri.LocalPath);
  }
}
