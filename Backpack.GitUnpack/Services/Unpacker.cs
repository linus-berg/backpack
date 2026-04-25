using Backpack.GitUnpack.Models;
using Core.Kernel;

namespace Backpack.GitUnpack.Services;

public class Unpacker {
  private readonly string archive_dir_;
  private readonly string in_dir_;
  private readonly ILogger<Unpacker> logger_;
  private readonly string repo_dir_;

  /* TODO: This class should probably be cleaned up */
  public Unpacker(ILogger<Unpacker> logger) {
    logger_ = logger;
    in_dir_ = Environment.GetEnvironmentVariable("GIT_BUNDLE_INPUT") ??
              throw new InvalidOperationException("GIT_BUNDLE_INPUT not set.");
    archive_dir_ = Environment.GetEnvironmentVariable("GIT_BUNDLE_ARCHIVE") ??
                   throw new InvalidOperationException(
                     "GIT_BUNDLE_ARCHIVE not set."
                   );
    repo_dir_ = Environment.GetEnvironmentVariable("GIT_BUNDLE_REPOS") ??
                throw new InvalidOperationException(
                  "GIT_BUNDLE_REPOS not set."
                );
  }

  public async Task<int> ProcessBundles(CancellationToken token) {
    IEnumerable<GitBundle> bundles =
      Directory.GetFiles(in_dir_, "*", SearchOption.AllDirectories)
               .Select(
                 f => {
                   string relative_path = Path.GetRelativePath(in_dir_, f);
                   string? directory = Path.GetDirectoryName(relative_path);
                   if (string.IsNullOrEmpty(directory)) {
                     throw new ArgumentException(
                       $"Could not get directory of ${relative_path}"
                     );
                   }

                   return new GitBundle(f, directory);
                 }
               );


    await Parallel.ForEachAsync(
      bundles,
      token,
      async (git_bundle, cancellation_token) => {
        /* Order by To date */
        if (!Path.GetFileName(git_bundle.filepath)
                 .StartsWith("__IGNORE__")) {
          try {
            if (!await TryApplyBundle(git_bundle, cancellation_token)) {
              logger_.LogError("Failed to apply {Arg1Filepath}", git_bundle.filepath);
            }
          } catch (Exception e) {
            logger_.LogError(e, "Failed to apply {Arg1Filepath}", git_bundle.filepath);
          }
        }
      }
    );

    return 0;
  }

  private async Task<bool> TryApplyBundle(GitBundle bundle,
                                          CancellationToken token = default) {
    logger_.LogInformation("Processing: {BundleFilepath}", bundle.filepath);
    bool is_valid = await CheckIfValid(bundle, token);
    if (!is_valid) {
      return false;
    }

    bool success = false;
    if (!Directory.Exists(bundle.repository_dir)) {
      string dir = Path.Join(repo_dir_, bundle.owner);
      Directory.CreateDirectory(dir);
      success = await Bin.Execute(
                  "git",
                  args => {
                    args.Add("clone");
                    args.Add("--mirror");
                    args.Add(bundle.filepath);
                    args.Add(bundle.repository);
                  },
                  logger_,
                  dir,
                  token: token
                );
    } else {
      success = await Bin.Execute(
                  "git",
                  args => {
                    args.Add("remote");
                    args.Add("update");
                  },
                  logger_,
                  bundle.repository_dir,
                  0,
                  token
                );
    }

    if (success) {
      await Cleanup(bundle);
    }

    return success;
  }

  private async Task<bool> CheckIfValid(GitBundle bundle,
                                        CancellationToken token = default) {
    /* If initial dir does not exist, it must be valid. */
    if (!Directory.Exists(bundle.repository_dir)) {
      return true;
    }

    /* If incremental bundle validate bundle */
    bool is_valid =
      await Bin.Execute(
        "git",
        args => {
          args.Add("bundle");
          args.Add("verify");
          args.Add(bundle.filepath);
        },
        logger_,
        bundle.repository_dir,
        0,
        token
      );
    return is_valid;
  }

  private async Task Cleanup(GitBundle bundle) {
    if (!Directory.Exists(bundle.repository_dir)) {
      return;
    }

    await UpdateServerInfo(bundle);
    MoveToArchive(bundle);
  }

  private async Task UpdateServerInfo(GitBundle bundle,
                                      CancellationToken token = default) {
    await Bin.Execute(
      "git",
      args => { args.Add("update-server-info"); },
      logger_,
      bundle.repository_dir,
      0,
      token
    );
  }

  private void MoveToArchive(GitBundle bundle) {
    string dir = Path.Join(archive_dir_, bundle.owner);
    Directory.CreateDirectory(dir);
    File.Move(
      bundle.filepath,
      Path.Join(
        archive_dir_,
        bundle.owner,
        Path.GetFileName(bundle.filepath)
      ),
      true
    );
  }
}