// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Collector.Kernel;
using Core.Kernel;
using Minio.Exceptions;
using Polly;
using Polly.Registry;

namespace Collector.Git;

/// <summary>
///   Handles git mirroring operations.
/// </summary>
public class Git {
  private static readonly ConcurrentDictionary<string, SemaphoreSlim> S_LOCKS_ =
    new();

  private readonly string bundle_dir_;
  private readonly string dir_;
  private readonly FileSystem fs_;
  private readonly ResiliencePipeline<bool> git_timeout_;
  private readonly ILogger<Git> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Git" /> class.
  /// </summary>
  /// <param name="fs">The file system.</param>
  /// <param name="polly">The resilience pipeline provider.</param>
  /// <param name="logger">The logger.</param>
  public Git(FileSystem fs, ResiliencePipelineProvider<string> polly,
             ILogger<Git> logger) {
    fs_ = fs;
    dir_ = fs_.GetModuleDir("git", true);
    bundle_dir_ = Path.GetFullPath(Path.Join(dir_, "/tmp", "/bundles"));
    git_timeout_ = polly.GetPipeline<bool>("git-timeout");
    logger_ = logger;
    ConfigureProxy();
  }

  /// <summary>
  ///   Configures the git proxy if specified in environment variables.
  /// </summary>
  private void ConfigureProxy() {
    string? proxy = Environment.GetEnvironmentVariable("HTTPS_PROXY");

    if (string.IsNullOrEmpty(proxy)) {
      return;
    }

    Bin
      .Execute(
        "git",
        args => {
          args.Add("config");
          args.Add("--global");
          args.Add("http.proxy");
          args.Add(proxy);
        },
        logger_
      )
      .Wait();
  }

  /// <summary>
  ///   Mirrors a remote repository.
  /// </summary>
  /// <param name="remote">The remote repository URL.</param>
  /// <param name="token">The cancellation token.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the mirroring was
  ///   successful.
  /// </returns>
  public async Task<bool> Mirror(string remote, CancellationToken token) {
    Repository repository = new(remote, dir_);

    SemaphoreSlim repo_lock =
      S_LOCKS_.GetOrAdd(repository.local_path, _ => new SemaphoreSlim(1, 1));
    await repo_lock.WaitAsync(token);

    try {
      logger_.LogDebug("{Remote}: Starting", remote);
      CleanStaleResources(repository.local_path);
      CleanStaleResources(Path.Join(bundle_dir_, repository.owner));

      bool success =
        await git_timeout_.ExecuteAsync(
          async (state, lambda_token) =>
            await CloneOrUpdateLocalMirror(state, lambda_token),
          repository,
          token
        );
      logger_.LogDebug("{Remote}: {Success}", remote, success);
      if (success) {
        logger_.LogDebug("{Remote}: Creating bundle", remote);
        await CreateIncrementalGitBundle(repository, token);
      }

      return success;
    } finally {
      repo_lock.Release();
    }
  }

  /// <summary>
  ///   Cleans up stale git lock files and temporary files from the specified path.
  /// </summary>
  /// <param name="path">The path to clean.</param>
  private void CleanStaleResources(string path) {
    if (!Directory.Exists(path)) {
      return;
    }

    string[] patterns = { "*.lock", "tmp_*" };
    foreach (string pattern in patterns) {
      string[] files =
        Directory.GetFiles(path, pattern, SearchOption.AllDirectories);
      foreach (string file in files) {
        try {
          logger_.LogWarning("Removing stale resource: {File}", file);
          File.Delete(file);
        } catch (Exception ex) {
          logger_.LogError(
            ex,
            "Failed to remove stale resource: {File}",
            file
          );
        }
      }
    }
  }

  /// <summary>
  ///   Clones or updates the local mirror of a repository.
  /// </summary>
  /// <param name="repository">The repository to clone or update.</param>
  /// <param name="token">The cancellation token.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the operation was
  ///   successful.
  /// </returns>
  private async Task<bool> CloneOrUpdateLocalMirror(
    Repository repository, CancellationToken token = default) {
    if (!Directory.Exists(repository.local_path)) {
      Directory.CreateDirectory(Path.Join(dir_, repository.owner));
      // Clone the mirror repository
      logger_.LogInformation(
        "{RepositoryRemote}: Cloning initial repository",
        repository.remote
      );

      return await Bin.Execute(
               "git",
               args => {
                 args.Add("clone");
                 args.Add("--mirror");
                 args.Add(repository.remote);
                 args.Add(repository.local_path);
               },
               logger_,
               token: token
             );
    }

    // Fetch updates to the mirror repository
    logger_.LogDebug("{RepositoryRemote}: Fetching updates", repository.remote);
    return await Bin.Execute(
             "git",
             args => {
               args.Add("remote");
               args.Add("update");
               args.Add("--prune");
             },
             logger_,
             repository.local_path,
             token: token
           );
  }

  /// <summary>
  ///   Creates an incremental git bundle for a repository and pushes it to storage.
  /// </summary>
  /// <param name="repository">The repository to bundle.</param>
  /// <param name="token">The cancellation token.</param>
  /// <returns>A task that represents the asynchronous operation.</returns>
  private async Task CreateIncrementalGitBundle(Repository repository,
                                                CancellationToken token) {
    string bundle_dir = Path.Join(bundle_dir_, repository.owner);
    if (!Directory.Exists(bundle_dir)) {
      Directory.CreateDirectory(bundle_dir);
    }

    /* Get latest update from storage */
    logger_.LogDebug(
      "{RepositoryRemote}: Getting timestamp",
      repository.remote
    );

    string bundle_file_name = repository.name;
    string bundle_file_path = Path.Combine(bundle_dir, bundle_file_name);

    // Create an incremental bundle
    logger_.LogInformation(
      "{RepositoryRemote}: Bundling ",
      repository.remote
    );
    logger_.LogDebug(
      "{RepositoryRemote}: Dirs {RepositoryLocalPath} - {RepositoryDirectory}",
      repository.remote,
      repository.local_path,
      repository.directory
    );

    bool success = await Bin.Execute(
                     "git",
                     args => {
                       args.Add("bundle");
                       args.Add("create");
                       args.Add(bundle_file_path);
                       args.Add("--all");
                     },
                     logger_,
                     repository.local_path,
                     0,
                     token
                   );
    logger_.LogDebug(
      "{RepositoryRemote}: Bundle result {Success}",
      repository.remote,
      success
    );
    if (success) {
      logger_.LogInformation(
        "{RepositoryRemote}: Pushing {BundleFilePath} to S3",
        repository.remote,
        bundle_file_path
      );
      bool uploaded = await PushToStorage(bundle_file_path);
      if (uploaded) {
        await fs_.CreateDeltaLink(
          "git",
          $"git://{Path.GetRelativePath(bundle_dir_, bundle_file_path)}"
        );
      } else {
        logger_.LogError(
          "Failed to push {BundleFilePath} to storage",
          bundle_file_path
        );
      }
    }

    /* Always delete git bundle at the end */
    logger_.LogInformation("Deleting {BundleFilePath}", bundle_file_path);
    if (File.Exists(bundle_file_path)) {
      try {
        File.Delete(bundle_file_path);
      } catch (Exception ex) {
        logger_.LogError(
          ex,
          "Could not delete {BundleFilePath}",
          bundle_file_path
        );
      }
    }
  }

  /// <summary>
  ///   Pushes the git bundle to storage.
  /// </summary>
  /// <param name="bundle_file_path">The path to the bundle file.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the upload was
  ///   successful.
  /// </returns>
  private async Task<bool> PushToStorage(string bundle_file_path) {
    if (!File.Exists(bundle_file_path)) {
      throw new FileNotFoundException($"{bundle_file_path} not found on disk.");
    }

    /* Open bundle and stream to S3 */
    logger_.LogDebug("Opening: {BundleFilePath}", bundle_file_path);
    await using Stream stream = File.OpenRead(bundle_file_path);
    string storage_path =
      Path.Join("git", Path.GetRelativePath(bundle_dir_, bundle_file_path));

    /* Try uploading to S3. */
    bool success = await fs_.PutFile(storage_path, stream);
    stream.Close();

    /* Delete bundle file on disk */
    File.Delete(bundle_file_path);

    /* If S3 upload failed */
    if (!success) {
      throw new MinioException($"Failed to upload {bundle_file_path}");
    }

    return success;
  }
}