// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Collector.Kernel.Storage.Common;
using Collector.Kernel.Storage.Minio;
using Core.Kernel;
using Polly;
using Polly.Registry;

namespace Collector.Kernel;

/// <summary>
/// Provides high-level file system operations abstraction using a Minio storage backend.
/// </summary>
public class FileSystem {
  private readonly string? base_dir_ =
    Configuration.GetBackpackVariable(CoreVariables.BP_COLLECTOR_DIRECTORY);

  private readonly MinioStorage storage_backend_;
  private readonly ResiliencePipeline<bool> storage_pipeline_;

  /// <summary>
  /// Initializes a new instance of the <see cref="FileSystem"/> class.
  /// </summary>
  /// <param name="storage_backend">The Minio storage backend to use.</param>
  /// <param name="polly">The resilience pipeline provider.</param>
  public FileSystem(MinioStorage storage_backend,
                    ResiliencePipelineProvider<string> polly) {
    storage_backend_ = storage_backend;
    storage_pipeline_ = polly.GetPipeline<bool>("storage-pipeline");
  }

  /// <summary>
  /// Checks if a file or directory exists at the specified path.
  /// </summary>
  /// <param name="path">The path to check.</param>
  /// <returns>True if it exists; otherwise, false.</returns>
  public async Task<bool> Exists(string path) {
    return await storage_backend_.ExistsAsync(path);
  }

  /// <summary>
  /// Retrieves a list of files matching the specified search pattern.
  /// </summary>
  /// <param name="search_pattern">The pattern to match files against.</param>
  /// <returns>A collection of matching file specifications.</returns>
  public async Task<IReadOnlyCollection<FileSpec>> GetFileList(
    string search_pattern) {
    return await storage_backend_.GetFileListAsync(search_pattern);
  }

  /// <summary>
  /// Retrieves a paged list of files matching the specified search pattern.
  /// </summary>
  /// <param name="search_pattern">The pattern to match files against.</param>
  /// <param name="page_size">The maximum number of files per page.</param>
  /// <returns>A paged result of matching file specifications.</returns>
  public async Task<PagedFileListResult> GetPagedFileList(
    string search_pattern, int page_size = 10000) {
    return await storage_backend_.GetPagedFileListAsync(
             page_size,
             search_pattern
           );
  }

  /// <summary>
  /// Deletes the file at the specified path.
  /// </summary>
  /// <param name="path">The path of the file to delete.</param>
  /// <returns>True if the file was deleted; otherwise, false.</returns>
  public async Task<bool> Delete(string path) {
    return await storage_backend_.DeleteFileAsync(path);
  }

  /// <summary>
  /// Renames or moves a file from one path to another.
  /// </summary>
  /// <param name="a">The source path.</param>
  /// <param name="b">The destination path.</param>
  /// <returns>True if the rename was successful; otherwise, false.</returns>
  public async Task<bool> Rename(string a, string b) {
    return await storage_backend_.RenameFileAsync(a, b);
  }

  /// <summary>
  /// Opens a stream for reading the file at the specified path.
  /// </summary>
  /// <param name="path">The path of the file to read.</param>
  /// <returns>A stream for reading the file.</returns>
  public async Task<Stream> GetStream(string path) {
    return await storage_backend_.GetFileStreamAsync(path, StreamMode.READ);
  }

  /// <summary>
  /// Reads the entire content of a file as a string.
  /// </summary>
  /// <param name="path">The path of the file to read.</param>
  /// <returns>The string content of the file.</returns>
  public async Task<string> GetString(string path) {
    return await storage_backend_.GetFileContentsAsync(path);
  }

  /// <summary>
  /// Writes a string to a file at the specified path.
  /// </summary>
  /// <param name="path">The path where the string will be written.</param>
  /// <param name="content">The string content to write.</param>
  /// <returns>True if the operation was successful; otherwise, false.</returns>
  public async Task<bool> PutString(string path, string content) {
    return await storage_pipeline_.ExecuteAsync(
             static async (state, _) =>
               await state.storage_backend_.SaveFileAsync(
                 state.path,
                 state.content
               ),
             (storage_backend_, path, content)
           );
  }

  /// <summary>
  /// Writes a stream to a file at the specified path.
  /// </summary>
  /// <param name="path">The path where the stream will be written.</param>
  /// <param name="stream">The stream content to write.</param>
  /// <returns>True if the operation was successful; otherwise, false.</returns>
  public async Task<bool> PutFile(string path, Stream stream) {
    return await storage_pipeline_.ExecuteAsync(
             static async (state, token) =>
               await state.storage_backend_.SaveFileAsync(
                 state.path,
                 state.stream,
                 token
               ),
             (storage_backend_, path, stream)
           );
  }


  /// <summary>
  /// Gets the daily deposit path for deltas of a specific module.
  /// </summary>
  /// <param name="module">The module name.</param>
  /// <returns>The relative path for the daily delta deposit.</returns>
  private string GetDeltaDeposit(string module) {
    string daily_deposit = Path.Join("delta", module);
    return Path.Join(daily_deposit, DateTime.UtcNow.ToString("yyyy_MM_dd"));
  }

  /// <summary>
  /// Creates a delta link for a given module and artifact URI.
  /// </summary>
  /// <param name="module">The module name.</param>
  /// <param name="uri_str">The URI of the artifact.</param>
  /// <returns>True if the link was created; otherwise, false.</returns>
  public async Task<bool> CreateDeltaLink(string module, string uri_str) {
    Uri uri = new(uri_str);
    string location = GetDiskLocation(uri);
    string daily_deposit = GetDeltaDeposit(module);
    string link = Path.Join(daily_deposit, location);
    string target = GetArtifactPath(module, uri_str);
    return await CreateS3Link(link, target);
  }

  /// <summary>
  /// Creates a link in S3 by writing a target path into a link file.
  /// </summary>
  /// <param name="link">The link file path.</param>
  /// <param name="target">The target path.</param>
  /// <returns>True if the operation was successful; otherwise, false.</returns>
  private async Task<bool> CreateS3Link(string link, string target) {
    return await PutString(link, target);
  }

  /// <summary>
  /// Gets the full path for an artifact based on its module and URI.
  /// </summary>
  /// <param name="module">The module name.</param>
  /// <param name="uri_str">The URI of the artifact.</param>
  /// <returns>The full storage path for the artifact.</returns>
  public string GetArtifactPath(string module, string uri_str) {
    Uri uri = new(uri_str);
    string location = GetDiskLocation(uri);
    return GetModulePath(module, location);
  }

  /// <summary>
  /// Retrieves the size of a file in bytes.
  /// </summary>
  /// <param name="filepath">The path of the file.</param>
  /// <returns>The size of the file in bytes.</returns>
  /// <exception cref="InvalidOperationException">Thrown when file information cannot be retrieved.</exception>
  public async Task<long> GetFileSize(string filepath) {
    FileSpec spec = await storage_backend_.GetFileInfoAsync(filepath) ??
                    throw new InvalidOperationException();
    return spec.size;
  }

  /// <summary>
  /// Determines the disk location based on a URI.
  /// </summary>
  /// <param name="uri">The URI to parse.</param>
  /// <returns>A string representing the disk location.</returns>
  private string GetDiskLocation(Uri uri) {
    return $"{uri.Host}{CleanFilepath(uri.LocalPath)}";
  }

  /// <summary>
  /// Cleans a file path by removing special markers.
  /// </summary>
  /// <param name="location">The file path to clean.</param>
  /// <returns>The cleaned file path.</returns>
  private string CleanFilepath(string location) {
    return Regex.Replace(location, @"\/-\/", "/");
  }

  /// <summary>
  /// Combines a module name and a file path into a module-specific path.
  /// </summary>
  /// <param name="module">The module name.</param>
  /// <param name="filepath">The file path.</param>
  /// <returns>The combined path.</returns>
  private string GetModulePath(string module, string filepath) {
    return Path.Join(module, filepath);
  }

  /// <summary>
  /// Gets the directory path for a module, optionally creating it.
  /// </summary>
  /// <param name="module">The module name.</param>
  /// <param name="create">Whether to create the directory if it doesn't exist.</param>
  /// <returns>The full directory path for the module.</returns>
  public string GetModuleDir(string module, bool create = false) {
    string dir = Path.Join(base_dir_, module);
    if (create) {
      Directory.CreateDirectory(dir);
    }

    return dir;
  }
}
