// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using Collector.Kernel.Storage.Common;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Collector.Kernel.Storage.Minio;

/// <summary>
///   Provides storage operations using Minio.
/// </summary>
public class MinioStorage : IDisposable {
  private readonly string bucket_;
  private readonly ILogger logger_;
  private readonly bool should_auto_create_bucket_;
  private bool bucket_exists_checked_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="MinioStorage" /> class.
  /// </summary>
  /// <param name="options">The Minio storage options.</param>
  /// <param name="logger">The logger.</param>
  public MinioStorage(MinioStorageOptions options,
                      ILogger<MinioStorage> logger) {
    if (options == null) {
      throw new ArgumentNullException(nameof(options));
    }

    logger_ = logger;

    (IMinioClient client, string bucket) = CreateClient(options);
    this.client = client;
    bucket_ = bucket;
    should_auto_create_bucket_ = options.auto_create_bucket;
  }

  /// <summary>
  ///   Gets the Minio client.
  /// </summary>
  public IMinioClient client { get; }

  /// <inheritdoc />
  public void Dispose() {
  }

  /// <summary>
  ///   Ensures that the bucket exists.
  /// </summary>
  private async Task EnsureBucketExists() {
    if (!should_auto_create_bucket_ || bucket_exists_checked_) {
      return;
    }

    logger_.LogTrace("Checking if bucket {Bucket} exists", bucket_);
    bool found =
      await client.BucketExistsAsync(
        new BucketExistsArgs().WithBucket(bucket_)
      );
    if (!found) {
      logger_.LogInformation("Creating {Bucket}", bucket_);
      await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket_));
      logger_.LogInformation("Created {Bucket}", bucket_);
    }

    bucket_exists_checked_ = true;
  }

  /// <summary>
  ///   Gets a file stream for the specified path.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the file stream.</returns>
  [Obsolete(
    $"Use {nameof(GetFileStreamAsync)} with {nameof(FileAccess)} instead to define read or write behaviour of stream"
  )]
  public Task<Stream> GetFileStreamAsync(string path,
                                         CancellationToken cancellation_token =
                                           default) {
    return GetFileStreamAsync(path, StreamMode.READ, cancellation_token);
  }

  /// <summary>
  ///   Gets a file stream for the specified path and stream mode.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <param name="stream_mode">The stream mode.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the file stream.</returns>
  public async Task<Stream> GetFileStreamAsync(string path,
                                               StreamMode stream_mode,
                                               CancellationToken
                                                 cancellation_token = default) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    if (stream_mode is StreamMode.WRITE) {
      throw new NotSupportedException(
        $"Stream mode {stream_mode} is not supported."
      );
    }

    await EnsureBucketExists();

    string normalized_path = NormalizePath(path);
    logger_.LogTrace("Getting file stream for {Path}", normalized_path);

    try {
      Stream result = new MemoryStream();
      await client.GetObjectAsync(
        new GetObjectArgs().WithBucket(bucket_)
                           .WithObject(normalized_path)
                           .WithCallbackStream(
                             async (stream, _) =>
                               await stream
                                 .CopyToAsync(
                                   result,
                                   cancellation_token
                                 )
                           ),
        cancellation_token
      );
      result.Seek(0, SeekOrigin.Begin);
      return result;
    } catch (Exception ex) {
      logger_.LogError(
        ex,
        "Unable to get file stream for {Path}: {Message}",
        normalized_path,
        ex.Message
      );
      return null;
    }
  }

  /// <summary>
  ///   Gets information about the specified file.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the file info, or null if the file does not exist.</returns>
  public async Task<FileSpec?> GetFileInfoAsync(string path) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    await EnsureBucketExists();

    string normalized_path = NormalizePath(path);
    logger_.LogTrace("Getting file info for {Path}", normalized_path);

    try {
      ObjectStat? metadata = await client
                               .StatObjectAsync(
                                 new StatObjectArgs().WithBucket(bucket_)
                                   .WithObject(normalized_path)
                               );
      if (metadata.ExtraHeaders.TryGetValue(
            "X-Minio-Error-Code",
            out string error_code
          ) &&
          (string.Equals(error_code, "NoSuchBucket") ||
           string.Equals(error_code, "NoSuchKey"))) {
        return null;
      }

      return new FileSpec {
        path = normalized_path,
        size = metadata.Size,
        created = metadata.LastModified.ToUniversalTime(),
        modified = metadata.LastModified.ToUniversalTime()
      };
    } catch (Exception ex) {
      logger_.LogError(
        ex,
        "Unable to get file info for {Path}: {Message}",
        normalized_path,
        ex.Message
      );
      return null;
    }
  }

  /// <summary>
  ///   Checks if the specified file exists.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the file exists.</returns>
  public async Task<bool> ExistsAsync(string path) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    await EnsureBucketExists();

    string normalized_path = NormalizePath(path);
    logger_.LogTrace("Checking if {Path} exists", normalized_path);

    try {
      ObjectStat? metadata = await client
                               .StatObjectAsync(
                                 new StatObjectArgs().WithBucket(bucket_)
                                   .WithObject(normalized_path)
                               );
      if (metadata.ExtraHeaders.TryGetValue(
            "X-Minio-Error-Code",
            out string? error_code
          ) &&
          (string.Equals(error_code, "NoSuchBucket") ||
           string.Equals(error_code, "NoSuchKey"))) {
        return false;
      }

      return true;
    } catch (Exception ex) when (ex is ObjectNotFoundException
                                   or BucketNotFoundException) {
      logger_.LogDebug(
        ex,
        "Unable to check if {Path} exists: {Message}",
        normalized_path,
        ex.Message
      );
      return false;
    }
  }

  /// <summary>
  ///   Saves a file from a stream.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <param name="stream">The file content stream.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the file was successfully saved.</returns>
  public async Task<bool> SaveFileAsync(string path, Stream stream,
                                        CancellationToken cancellation_token =
                                          default) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    if (stream == null) {
      throw new ArgumentNullException(nameof(stream));
    }

    await EnsureBucketExists();

    string normalized_path = NormalizePath(path);
    logger_.LogTrace("Saving {Path}", normalized_path);

    string? tempfile = null;

    try {
      using Stream? seekable_stream = await GetSeekableStream(stream);

      PutObjectArgs? args = new PutObjectArgs()
                            .WithBucket(bucket_)
                            .WithObject(normalized_path)
                            .WithObjectSize(seekable_stream.Length);

      // Minio does not allow uploading empty streams: https://github.com/minio/minio-dotnet/issues/801
      if (seekable_stream.Length > 0) {
        args.WithStreamData(seekable_stream);
      } else {
        // Work around bug by putting an empty file instead
        tempfile = Path.GetTempFileName();
        args.WithFileName(tempfile);
      }

      await client.PutObjectAsync(args, cancellation_token);

      return true;
    } catch (Exception ex) {
      logger_.LogError(
        ex,
        "Error saving {Path}: {Message}",
        normalized_path,
        ex.Message
      );
      throw;
    } finally {
      if (tempfile != null) {
        File.Delete(tempfile);
      }
    }
  }

  /// <summary>
  ///   Gets a seekable stream from the provided stream.
  /// </summary>
  /// <param name="stream">The original stream.</param>
  /// <returns>A seekable stream.</returns>
  private async Task<Stream> GetSeekableStream(Stream stream) {
    if (stream.CanSeek) {
      return stream;
    }

    FileStream? temp_file_stream = File.Create(
      Path.GetTempFileName(),
      8192,
      FileOptions.DeleteOnClose
    );
    await stream.CopyToAsync(temp_file_stream);
    temp_file_stream.Seek(0, SeekOrigin.Begin);

    return temp_file_stream;
  }

  /// <summary>
  ///   Renames a file.
  /// </summary>
  /// <param name="path">The current file path.</param>
  /// <param name="new_path">The new file path.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the file was successfully renamed.</returns>
  public async Task<bool> RenameFileAsync(string path, string new_path,
                                          CancellationToken cancellation_token =
                                            default) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    if (string.IsNullOrEmpty(new_path)) {
      throw new ArgumentNullException(nameof(new_path));
    }

    await EnsureBucketExists();

    string normalized_path = NormalizePath(path);
    string normalized_new_path = NormalizePath(new_path);
    logger_.LogInformation(
      "Renaming {Path} to {NewPath}",
      normalized_path,
      normalized_new_path
    );

    return await CopyFileAsync(
             normalized_path,
             normalized_new_path,
             cancellation_token
           ) &&
           await DeleteFileAsync(normalized_path, cancellation_token);
  }

  /// <summary>
  ///   Gets the contents of a file as a string.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the file contents.</returns>
  public async Task<string> GetFileContentsAsync(string path) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

#pragma warning disable CS0618 // Type or member is obsolete
    await using Stream? stream = await GetFileStreamAsync(path);
#pragma warning restore CS0618 // Type or member is obsolete
    if (stream != null) {
      return await new StreamReader(stream).ReadToEndAsync();
    }

    return null;
  }

  /// <summary>
  ///   Saves file contents from a string.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <param name="contents">The file contents.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the file was successfully saved.</returns>
  public Task<bool> SaveFileAsync(string path, string contents) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    return SaveFileAsync(
      path,
      new MemoryStream(Encoding.UTF8.GetBytes(contents ?? string.Empty))
    );
  }

  /// <summary>
  ///   Copies a file.
  /// </summary>
  /// <param name="path">The source file path.</param>
  /// <param name="target_path">The target file path.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the file was successfully copied.</returns>
  public async Task<bool> CopyFileAsync(string path, string target_path,
                                        CancellationToken cancellation_token =
                                          default) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    if (string.IsNullOrEmpty(target_path)) {
      throw new ArgumentNullException(nameof(target_path));
    }

    await EnsureBucketExists();

    string normalized_path = NormalizePath(path);
    string normalized_target_path = NormalizePath(target_path);
    logger_.LogInformation(
      "Copying {Path} to {TargetPath}",
      normalized_path,
      normalized_target_path
    );

    try {
      CopySourceObjectArgs? copy_source_args = new CopySourceObjectArgs()
                                               .WithBucket(bucket_)
                                               .WithObject(normalized_path);

      await client.CopyObjectAsync(
        new CopyObjectArgs()
          .WithBucket(bucket_)
          .WithObject(normalized_target_path)
          .WithCopyObjectSource(copy_source_args),
        cancellation_token
      );
      return true;
    } catch (Exception ex) {
      logger_.LogError(
        ex,
        "Error copying {Path} to {TargetPath}: {Message}",
        normalized_path,
        normalized_target_path,
        ex.Message
      );
      return false;
    }
  }

  /// <summary>
  ///   Deletes a file.
  /// </summary>
  /// <param name="path">The file path.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the file was successfully deleted.</returns>
  public async Task<bool> DeleteFileAsync(string path,
                                          CancellationToken cancellation_token =
                                            default) {
    if (string.IsNullOrEmpty(path)) {
      throw new ArgumentNullException(nameof(path));
    }

    await EnsureBucketExists();

    string normalized_path = NormalizePath(path);
    logger_.LogTrace("Deleting {Path}", normalized_path);

    try {
      await client
        .RemoveObjectAsync(
          new RemoveObjectArgs().WithBucket(bucket_)
                                .WithObject(normalized_path),
          cancellation_token
        );
      return true;
    } catch (Exception ex) {
      logger_.LogError(
        ex,
        "Unable to delete {Path}: {Message}",
        normalized_path,
        ex.Message
      );
      return false;
    }
  }

  /// <summary>
  ///   Deletes files matching a search pattern.
  /// </summary>
  /// <param name="search_pattern">The search pattern.</param>
  /// <param name="cancellation">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the number of files deleted.</returns>
  public async Task<int> DeleteFilesAsync(string search_pattern = null,
                                          CancellationToken cancellation =
                                            default) {
    await EnsureBucketExists();

    logger_.LogInformation(
      "Deleting files matching {SearchPattern}",
      search_pattern
    );

    int count = 0;
    PagedFileListResult result =
      await GetPagedFileListAsync(250, search_pattern, cancellation);
    do {
      if (result.files.Count == 0) {
        break;
      }

      RemoveObjectsArgs? args = new RemoveObjectsArgs().WithBucket(bucket_)
        .WithObjects(
          result.files
                .Select(spec => NormalizePath(spec.path))
                .ToList()
        );

      IList<DeleteError>? response =
        await client.RemoveObjectsAsync(args, cancellation);
      count += result.files.Count;
      foreach (DeleteError? error in response) {
        count--;
        logger_.LogError(
          "Error deleting {Path}: {Message}",
          error.Key,
          error.Message
        );
      }
    } while (await result.NextPageAsync());

    logger_.LogTrace(
      "Finished deleting {FileCount} files matching {SearchPattern}",
      count,
      search_pattern
    );
    return count;
  }

  /// <summary>
  ///   Gets a list of files matching a search pattern.
  /// </summary>
  /// <param name="search_pattern">The search pattern.</param>
  /// <param name="limit">The maximum number of files to return.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the list of files.</returns>
  public async Task<IReadOnlyCollection<FileSpec>> GetFileListAsync(
    string search_pattern = null, int? limit = null,
    CancellationToken cancellation_token = default) {
    List<FileSpec> files = new();
    limit ??= int.MaxValue;
    PagedFileListResult result =
      await GetPagedFileListAsync(
        limit.Value,
        search_pattern,
        cancellation_token
      );
    do {
      files.AddRange(result.files);
    } while (result.has_more &&
             files.Count < limit.Value &&
             await result.NextPageAsync());

    return files;
  }

  /// <summary>
  ///   Gets a paged list of files matching a search pattern.
  /// </summary>
  /// <param name="page_size">The page size.</param>
  /// <param name="search_pattern">The search pattern.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the paged list of files.</returns>
  public async Task<PagedFileListResult> GetPagedFileListAsync(
    int page_size = 100, string search_pattern = null,
    CancellationToken cancellation_token = default) {
    if (page_size <= 0) {
      return PagedFileListResult.S_EMPTY;
    }

    await EnsureBucketExists();

    PagedFileListResult result =
      new(_ => GetFiles(search_pattern, 1, page_size, cancellation_token));
    await result.NextPageAsync();
    return result;
  }

  /// <summary>
  ///   Gets a page of files.
  /// </summary>
  /// <param name="search_pattern">The search pattern.</param>
  /// <param name="page">The page number.</param>
  /// <param name="page_size">The page size.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the next page result.</returns>
  private async Task<NextPageResult> GetFiles(string search_pattern, int page,
                                              int page_size,
                                              CancellationToken
                                                cancellation_token) {
    int paging_limit = page_size;
    int skip = (page - 1) * paging_limit;
    if (paging_limit < int.MaxValue) {
      paging_limit++;
    }

    List<FileSpec> list =
      (await GetFileListAsync(
         search_pattern,
         paging_limit,
         skip,
         cancellation_token
       )).ToList();
    bool has_more = false;
    if (list.Count == paging_limit) {
      has_more = true;
      list.RemoveAt(paging_limit - 1);
    }

    return new NextPageResult {
      success = true,
      has_more = has_more,
      files = list,
      next_page_func = has_more
                         ? _ => GetFiles(
                           search_pattern,
                           page + 1,
                           page_size,
                           cancellation_token
                         )
                         : null
    };
  }

  /// <summary>
  ///   Gets a list of files matching a search pattern with limit and skip.
  /// </summary>
  /// <param name="search_pattern">The search pattern.</param>
  /// <param name="limit">The limit.</param>
  /// <param name="skip">The skip.</param>
  /// <param name="cancellation_token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the list of files.</returns>
  private async Task<List<FileSpec>> GetFileListAsync(
    string search_pattern = null, int? limit = null, int? skip = null,
    CancellationToken cancellation_token = default) {
    if (limit is <= 0) {
      return new List<FileSpec>();
    }

    List<Item> list = new();
    SearchCriteria criteria = GetRequestCriteria(search_pattern);

    logger_.LogTrace(
      s => s.Property("SearchPattern", search_pattern)
            .Property("Limit", limit)
            .Property("Skip", skip),
      "Getting file list recursively matching {Prefix} and {Pattern}...",
      criteria.prefix,
      criteria.pattern
    );
    await foreach (Item? item in client.ListObjectsEnumAsync(
                     new ListObjectsArgs().WithBucket(bucket_)
                                          .WithPrefix(criteria.prefix)
                                          .WithRecursive(true),
                     cancellation_token
                   )) {
      if (item.IsDir) {
        continue;
      }

      if (criteria.pattern != null && !criteria.pattern.IsMatch(item.Key)) {
        logger_.LogTrace("Skipping {Path}: Doesn't match pattern", item.Key);
        continue;
      }

      list.Add(item);
    }

    if (skip.HasValue) {
      list = list.Skip(skip.Value).ToList();
    }

    if (limit.HasValue) {
      list = list.Take(limit.Value).ToList();
    }

    return list.Select(
                 blob => new FileSpec {
                   path = blob.Key,
                   size = (long)blob.Size,
                   modified = DateTime.Parse(blob.LastModified),
                   created = DateTime.Parse(blob.LastModified)
                 }
               )
               .ToList();
  }

  /// <summary>
  ///   Normalizes the path by replacing backslashes with forward slashes.
  /// </summary>
  /// <param name="path">The path to normalize.</param>
  /// <returns>The normalized path.</returns>
  private string NormalizePath(string path) {
    return path?.Replace('\\', '/');
  }

  /// <summary>
  ///   Gets the search criteria from a search pattern.
  /// </summary>
  /// <param name="search_pattern">The search pattern.</param>
  /// <returns>The search criteria.</returns>
  private SearchCriteria GetRequestCriteria(string search_pattern) {
    if (string.IsNullOrEmpty(search_pattern)) {
      return new SearchCriteria {
        prefix = string.Empty
      };
    }

    string normalized_search_pattern = NormalizePath(search_pattern);
    int wildcard_pos = normalized_search_pattern.IndexOf('*');
    bool has_wildcard = wildcard_pos >= 0;

    string prefix = normalized_search_pattern;
    Regex pattern_regex = null;

    if (has_wildcard) {
      pattern_regex =
        new Regex(
          $"^{Regex.Escape(normalized_search_pattern).Replace("\\*", ".*?")}$"
        );
      int slash_pos = normalized_search_pattern.LastIndexOf('/');
      prefix = slash_pos >= 0
                 ? normalized_search_pattern.Substring(0, slash_pos)
                 : string.Empty;
    }

    return new SearchCriteria {
      prefix = prefix,
      pattern = pattern_regex
    };
  }

  /// <summary>
  ///   Creates a Minio client from the provided options.
  /// </summary>
  /// <param name="options">The Minio storage options.</param>
  /// <returns>The Minio client and bucket name.</returns>
  private (IMinioClient Client, string Bucket) CreateClient(
    MinioStorageOptions options) {
    MinioConnectionBuilder connection =
      new(options.connection_string);

    string endpoint;
    bool secure;
    if (connection.end_point.StartsWith(
          "https://",
          StringComparison
            .OrdinalIgnoreCase
        )) {
      endpoint = connection.end_point.Substring(8);
      secure = true;
    } else {
      endpoint =
        connection.end_point.StartsWith(
          "http://",
          StringComparison
            .OrdinalIgnoreCase
        )
          ? connection.end_point.Substring(7)
          : connection.end_point;
      secure = false;
    }

    IMinioClient? client = new MinioClient()
                           .WithEndpoint(endpoint)
                           .WithCredentials(
                             connection.access_key,
                             connection.secret_key
                           );

    if (!string.IsNullOrEmpty(connection.region)) {
      client.WithRegion(connection.region ?? string.Empty);
    }

    client.Build();

    if (secure) {
      client.WithSSL();
    }

    return (client, connection.bucket);
  }
}
