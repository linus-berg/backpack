// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.Nuget;

/// <summary>
/// Background service for NuGet package processing.
/// </summary>
public class Worker : BackgroundService {
  private readonly ILogger<Worker> logger_;

  /// <summary>
  /// Initializes a new instance of the <see cref="Worker"/> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  public Worker(ILogger<Worker> logger) {
    logger_ = logger;
  }

  /// <summary>
  /// Executes the background task.
  /// </summary>
  /// <param name="stopping_token">The cancellation token.</param>
  /// <returns>A task that represents the background operation.</returns>
  protected override async Task ExecuteAsync(CancellationToken stopping_token) {
    while (!stopping_token.IsCancellationRequested) {
      await Task.Delay(1000, stopping_token);
    }
  }
}