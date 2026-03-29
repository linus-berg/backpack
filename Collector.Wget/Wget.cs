// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Core.Kernel;

namespace Collector.Wget;

/// <summary>
///   Handles wget mirroring operations.
/// </summary>
public class Wget {
  private readonly FileSystem fs_;
  private readonly ILogger<Wget> logger_;
  private readonly string wd_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Wget" /> class.
  /// </summary>
  /// <param name="logger">The logger.</param>
  /// <param name="fs">The file system.</param>
  public Wget(ILogger<Wget> logger, FileSystem fs) {
    logger_ = logger;
    fs_ = fs;
    wd_ = fs_.GetModuleDir("wget", true);
  }

  /// <summary>
  ///   Mirrors a remote location using wget.
  /// </summary>
  /// <param name="remote">The remote location.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the mirroring was
  ///   successful.
  /// </returns>
  public async Task<bool> Mirror(string remote) {
    return await Archive(remote);
  }

  /// <summary>
  ///   Executes the wget command to archive the remote location.
  /// </summary>
  /// <param name="remote">The remote location.</param>
  /// <returns>
  ///   A task that represents the asynchronous operation. The task result indicates whether the operation was
  ///   successful.
  /// </returns>
  private async Task<bool> Archive(string remote) {
    return await Bin.Execute(
             "wget",
             args => {
               args.Add("--mirror");
               args.Add("-k");
               args.Add("-p");
               args.Add("-E");
               args.Add("--no-parent");
               args.Add(remote);
             },
             logger_,
             wd_
           );
  }
}