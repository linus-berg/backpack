// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;

namespace Collector.Http;

/// <summary>
///   Represents a remote file accessible via HTTP.
/// </summary>
public class RemoteFile {
  private readonly HttpClient client_;
  private readonly FileSystem fs_;
  private readonly string url_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RemoteFile" /> class.
  /// </summary>
  /// <param name="client">The HTTP client to use for requests.</param>
  /// <param name="url">The URL of the remote file.</param>
  /// <param name="fs">The file system.</param>
  public RemoteFile(HttpClient client, string url, FileSystem fs) {
    client_ = client;
    url_ = url;
    fs_ = fs;
  }

  /// <summary>
  ///   Fetches the remote file and saves it to the specified path.
  /// </summary>
  /// <param name="path">The path where the file should be saved.</param>
  /// <param name="token">The cancellation token.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the file was successfully
  ///   retrieved.
  /// </returns>
  public async Task<bool> Get(string path, CancellationToken token = default) {
    using HttpResponseMessage response =
      await client_.GetAsync(
        url_,
        HttpCompletionOption.ResponseHeadersRead,
        token
      );
    if (!response.IsSuccessStatusCode) {
      return false;
    }

    /* Nothing is deleted on failure. PutFile buffers the whole body before it
       uploads and an S3 put is all or nothing, so a failed collect never leaves
       a partial object at path. Whatever is there is either the previous good
       copy or a concurrent collect's finished upload. */
    Stream body = await response.Content.ReadAsStreamAsync(token);
    bool result = await fs_.PutFile(path, body);

    if (!result) {
      throw new HttpRequestException($"{url_} failed to collect.");
    }

    return result;
  }
}
