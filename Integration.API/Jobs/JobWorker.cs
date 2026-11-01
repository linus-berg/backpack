using Core.Infrastructure.Services;
using Core.Kernel.Models;

namespace Integration.API.Jobs;

/// <summary>
///   Runs the jobs this replica started, in the background, so the request
///   that starts one returns at once. Only one job runs at a time anywhere,
///   so they run one after another here too. When the API shuts down, the
///   running job ends as interrupted.
/// </summary>
public sealed class JobWorker : BackgroundService {
  private readonly ILogger<JobWorker> logger_;
  private readonly JobQueue queue_;
  private readonly IServiceScopeFactory scopes_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="JobWorker" /> class.
  /// </summary>
  public JobWorker(JobQueue queue, IServiceScopeFactory scopes,
                   ILogger<JobWorker> logger) {
    queue_ = queue;
    scopes_ = scopes;
    logger_ = logger;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stopping) {
    await foreach (Job job in queue_.Reader.ReadAllAsync(stopping)) {
      using IServiceScope scope = scopes_.CreateScope();
      try {
        await scope.ServiceProvider.GetRequiredService<JobRunner>()
                   .Run(job, stopping);
      } catch (Exception e) {
        logger_.LogError(e, "Job {Job} could not run", job.id);
      }
    }
  }
}
