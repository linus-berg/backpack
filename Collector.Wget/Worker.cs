// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Collector.Wget;

/// <summary>
///   Background worker for the wget collector.
/// </summary>
public class Worker : BackgroundService {
  /// <summary>
  ///   Initializes a new instance of the <see cref="Worker" /> class.
  /// </summary>
  /// <param name="logger">The logger.</param>
  public Worker(ILogger<Worker> logger) {
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stopping_token) {
    while (!stopping_token.IsCancellationRequested) {
      await Task.Delay(1000, stopping_token);
    }
  }
}