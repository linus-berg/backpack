// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Library.Skopeo;
using Library.Skopeo.Exceptions;
using Library.Skopeo.Models;
using Minio.Exceptions;

namespace Collector.DockerArchive;

/// <summary>
///   Handles docker archive collection operations.
/// </summary>
public class Docker {
  private readonly string dir_;
  private readonly FileSystem fs_;
  private readonly ILogger<Docker> logger_;
  private readonly SkopeoClient skopeo_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Docker" /> class.
  /// </summary>
  /// <param name="fs">The file system.</param>
  /// <param name="skopeo">The Skopeo client.</param>
  /// <param name="logger">The logger.</param>
  public Docker(FileSystem fs, SkopeoClient skopeo, ILogger<Docker> logger) {
    fs_ = fs;
    skopeo_ = skopeo;
    dir_ = fs_.GetModuleDir("docker-archive", true);
    logger_ = logger;
  }

  /// <summary>
  ///   Gets a tar archive of a remote image.
  /// </summary>
  /// <param name="remote_image">The remote image location.</param>
  /// <param name="force">Forces a tar download even if file already exists.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the archive was
  ///   successfully retrieved.
  /// </returns>
  public async Task<bool> GetTarArchive(string remote_image, bool force = false) {
    SkopeoArchive archive;
    try {
      archive = await skopeo_.CopyToTar(remote_image, dir_, force);
    } catch (SkopeoArchiveExistsException ex) {
      /* Ignore if file exists */
      logger_.LogDebug(ex, "{RemoteImage} archive already exists", remote_image);
      return true;
    } catch (SkopeoTagMissingException ex) {
      /* Ignore if no tag found */
      logger_.LogWarning(ex, "{RemoteImage} image has no tag", remote_image);
      return true;
    }

    bool success = await PushToStorage(archive);
    if (!success) {
      throw new ApplicationException($"Failed to fetch {remote_image}");
    }

    success = await fs_.CreateDeltaLink(
                "docker-archive",
                $"docker-archive://{archive.TarWithHost}"
              );
    return success;
  }

  /// <summary>
  ///   Pushes the archive to storage.
  /// </summary>
  /// <param name="archive">The archive to push.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the archive was
  ///   successfully pushed.
  /// </returns>
  private async Task<bool> PushToStorage(SkopeoArchive archive) {
    if (!File.Exists(archive.TarPath)) {
      throw new FileNotFoundException($"{archive.TarPath} not found on disk.");
    }

    logger_.LogDebug("Opening: {BundleFilePath}", archive.TarPath);
    await using Stream stream = File.OpenRead(archive.TarPath);
    string storage_path = Path.Join("docker-archive", archive.TarWithHost);
    bool success = await fs_.PutFile(storage_path, stream);
    stream.Close();
    /* If S3 upload failed */
    if (!success) {
      throw new MinioException($"Failed to upload {archive.TarPath}");
    }

    return success;
  }
}