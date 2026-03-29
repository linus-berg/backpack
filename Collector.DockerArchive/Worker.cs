// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Collector.DockerArchive;

/// <summary>
///   Background worker for the docker archive collector.
/// </summary>
public class Worker : BackgroundService {
  private readonly ILogger<Worker> _logger;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Worker" /> class.
  /// </summary>
  /// <param name="logger">The logger.</param>
  public Worker(ILogger<Worker> logger) {
    _logger = logger;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    while (!stoppingToken.IsCancellationRequested) {
      await Task.Delay(1000, stoppingToken);
    }
  }
}