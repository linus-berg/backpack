using Core.Infrastructure.Services;
using Core.Kernel.Models;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Jobs;

/// <summary>
///   Starts jobs for the API's endpoints: 202 with the job when it started,
///   409 with the running job when another one is running.
/// </summary>
public sealed class JobStarter {
  private readonly ICoreDatabase db_;
  private readonly IJobService jobs_;
  private readonly JobQueue queue_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="JobStarter" /> class.
  /// </summary>
  public JobStarter(IJobService jobs, JobQueue queue, ICoreDatabase db) {
    jobs_ = jobs;
    queue_ = queue;
    db_ = db;
  }

  /// <summary>
  ///   Starts a job unless one is already running.
  /// </summary>
  /// <param name="kind">A <see cref="JobKinds" /> value.</param>
  /// <param name="processor">One processor, or null for every processor.</param>
  /// <param name="user">Who starts it.</param>
  public async Task<ActionResult> Start(string kind, string? processor,
                                        string user) {
    if (!JobKinds.IsKnown(kind)) {
      return new BadRequestObjectResult(
        new { message = $"Unknown job kind '{kind}'; use track or validate." }
      );
    }

    if (processor != null &&
        (await db_.GetProcessors()).All(p => p.id != processor)) {
      return new NotFoundObjectResult(
        new { message = $"There is no processor {processor}." }
      );
    }

    JobStart start = await jobs_.TryStart(kind, processor, user);
    if (start.job != null) {
      queue_.Enqueue(start.job);
      return new AcceptedResult($"/api/jobs/{start.job.id}", start.job);
    }

    return new ConflictObjectResult(
      new {
        message = start.running == null
                    ? "Another job is running. Wait for it to finish, or cancel it on the Jobs page."
                    : $"{JobRunner.Title(start.running)} is already running (started by {start.running.started_by}). Wait for it to finish, or cancel it on the Jobs page.",
        job = start.running
      }
    );
  }
}
