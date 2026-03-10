// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Pypi;

/// <summary>
/// Worker service for the PyPI processor.
/// </summary>
public class Worker : BackgroundService {
  /// <summary>
  /// Executes the worker's background operations.
  /// </summary>
  /// <param name="stoppingToken">The cancellation token.</param>
  /// <returns>A task that represents the execution operation.</returns>
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    while (!stoppingToken.IsCancellationRequested) {
      await Task.Delay(1000, stoppingToken);
    }
  }
}
