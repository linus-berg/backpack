// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;

namespace Collector.Rsync;

/// <summary>
///   Handles rsync mirroring operations.
/// </summary>
public class RSync {
  private readonly ILogger<RSync> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RSync" /> class.
  /// </summary>
  /// <param name="logger">The logger.</param>
  public RSync(ILogger<RSync> logger) {
    logger_ = logger;
  }

  /// <summary>
  ///   Mirrors a remote location using rsync.
  /// </summary>
  /// <param name="remote">The remote location.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the mirroring was successful.</returns>
  public async Task<bool> Mirror(string remote) {
    /* Bucket is hardcoded to rsync */
    return await Archive(remote, "rsync");
  }

  /// <summary>
  ///   Executes the rsync command to archive the remote location.
  /// </summary>
  /// <param name="remote">The remote location.</param>
  /// <param name="bucket">The target bucket.</param>
  /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
  private async Task<bool> Archive(string remote, string bucket) {
    return await Bin.Execute(
             "rsync-os",
             args => {
               args.Add(remote);
               args.Add(bucket);
             },
             logger_
           );
  }
}
