// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;

namespace Collector.Http;

/// <summary>
///   Represents a remote file accessible via HTTP.
/// </summary>
public class RemoteFile {
  private static readonly HttpClient S_CLIENT_ = new();
  private readonly FileSystem fs_;
  private readonly string url_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RemoteFile" /> class.
  /// </summary>
  /// <param name="url">The URL of the remote file.</param>
  /// <param name="fs">The file system.</param>
  public RemoteFile(string url, FileSystem fs) {
    url_ = url;
    fs_ = fs;
  }

  /// <summary>
  ///   Fetches the remote file and saves it to the specified path.
  /// </summary>
  /// <param name="path">The path where the file should be saved.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the file was successfully
  ///   retrieved.
  /// </returns>
  public async Task<bool> Get(string path) {
    HttpResponseMessage response =
      await S_CLIENT_.GetAsync(url_, HttpCompletionOption.ResponseHeadersRead);
    if (!response.IsSuccessStatusCode) {
      return false;
    }

    try {
      Stream? body = await response.Content.ReadAsStreamAsync();
      bool result = await fs_.PutFile(path, body);

      if (!result) {
        await ClearFile(path);
        throw new HttpRequestException($"{url_} failed to collect.");
      }

      return result;
    } catch (Exception) {
      await ClearFile(path);
      throw;
    }
  }

  /// <summary>
  ///   Clears the file from the file system.
  /// </summary>
  /// <param name="file">The file path to clear.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the file was successfully
  ///   cleared.
  /// </returns>
  private async Task<bool> ClearFile(string file) {
    if (!await fs_.Exists(file)) {
      return false;
    }

    Console.WriteLine($"Clearing {file}");
    await fs_.Delete(file);
    return true;
  }
}