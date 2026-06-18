using System.Net;
using Collector.Kernel;
using Collector.Kernel.Storage.Common;

namespace Collector.Huggingface;

/// <summary>
///   Represents a remote file accessible via HuggingFace protocol.
/// </summary>
public class RemoteFile {
  private const string C_HF_RESOLVE_URL_ =
    "https://huggingface.co/{0}/resolve/{1}/{2}";

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
    Uri uri = new(url_);
    string revision = GetQueryParam(uri, "revision") ?? "main";
    string model_id = GetQueryParam(uri, "modelId") ?? GetModelIdFallback(uri);
    string filename = GetFilename(uri, model_id);

    string hf_url = string.Format(C_HF_RESOLVE_URL_, model_id, revision, filename);

    // 1. Get remote ETAG using HEAD request
    using HttpRequestMessage head_request = new(HttpMethod.Head, hf_url);
    using HttpResponseMessage head_response =
      await client_.SendAsync(head_request, token);

    if (!head_response.IsSuccessStatusCode) {
      return false;
    }

    string? remote_etag = head_response.Headers.ETag?.Tag?.Trim('"');
    if (string.IsNullOrEmpty(remote_etag)) {
      // Fallback or handle missing ETAG
      // For HF, ETAG is usually present.
      remote_etag = head_response.Headers.GetValues("x-linked-etag")
                               .FirstOrDefault();
    }

    // 2. Check local ETAG from S3 metadata
    FileSpec? local_info = await fs_.GetFileInfo(path);
    if (local_info != null && !string.IsNullOrEmpty(remote_etag)) {
      if (local_info.metadata.TryGetValue("x-hf-etag", out string? hf_etag) && 
          hf_etag == remote_etag) {
        return true; // Already up to date
      }
    }

    // 3. Download if different
    using HttpResponseMessage response =
      await client_.GetAsync(
        hf_url,
        HttpCompletionOption.ResponseHeadersRead,
        token
      );
    if (!response.IsSuccessStatusCode) {
      return false;
    }

    try {
      Stream? body = await response.Content.ReadAsStreamAsync(token);
      
      // Store the HF ETAG in metadata
      Dictionary<string, string> metadata = new();
      if (!string.IsNullOrEmpty(remote_etag)) {
        metadata["X-HF-ETag"] = remote_etag;
      }

      bool result = await fs_.PutFile(path, body, metadata);

      if (!result) {
        await ClearFile(path);
        throw new HttpRequestException($"{hf_url} failed to collect.");
      }

      return result;
    } catch (Exception) {
      await ClearFile(path);
      throw;
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

  private string GetFilename(Uri uri, string model_id) {
    // The storage path is Host + LocalPath
    // modelId can be "org/repo" or "repo"
    // Example: hf://org/repo/path/to/file?modelId=org/repo
    // Host: org, Path: /repo/path/to/file
    // Combined: org/repo/path/to/file
    // If we remove "org/repo/" from the start, we get "path/to/file"

    string combined = $"{uri.Host}{uri.LocalPath}";
    if (combined.StartsWith(model_id, StringComparison.Ordinal)) {
      string filename = combined.Substring(model_id.Length);
      return filename.TrimStart('/');
    }

    return Path.GetFileName(uri.LocalPath);
  }

  private string GetModelIdFallback(Uri uri) {
    string host = uri.Host;
    string local_path = uri.LocalPath.TrimStart('/');
    int first_slash = local_path.IndexOf('/');
    if (first_slash < 0) {
      return host;
    }

    // Default to org/repo if available
    return $"{host}/{local_path.Substring(0, first_slash)}";
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
