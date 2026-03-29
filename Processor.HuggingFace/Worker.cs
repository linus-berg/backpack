// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Processor.HuggingFace;

/// <summary>
///   Worker for HuggingFace artifact processing.
/// </summary>
public class Worker : BackgroundService {
  private readonly ILogger<Worker> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Worker" /> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  public Worker(ILogger<Worker> logger) {
    logger_ = logger;
  }

  /// <summary>
  ///   Executes the background operation.
  /// </summary>
  /// <param name="stoppingToken">The stopping token.</param>
  /// <returns>A task that represents the execute operation.</returns>
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    logger_.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
    while (!stoppingToken.IsCancellationRequested) {
      await Task.Delay(1000, stoppingToken);
    }
  }
}