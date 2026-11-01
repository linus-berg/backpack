using Core.Kernel.Models;

namespace Core.Services;

/// <summary>
///   The outcome of starting a job: the new job, or the job that is already
///   running and blocks it.
/// </summary>
/// <param name="job">The started job, or null.</param>
/// <param name="running">The job that blocks it, if known.</param>
public sealed record JobStart(Job? job, Job? running);

/// <summary>
///   What a running job should do next.
/// </summary>
public enum JobSignal {
  /// <summary>Carry on.</summary>
  CONTINUE,

  /// <summary>An administrator asked to cancel it.</summary>
  CANCEL,

  /// <summary>
  ///   It is no longer running: it was marked interrupted, for example after
  ///   losing MongoDB for too long. Stop without changing it.
  /// </summary>
  LOST
}

/// <summary>
///   The outcome of a cancel request.
/// </summary>
public enum JobCancel {
  /// <summary>No such job.</summary>
  NOT_FOUND,

  /// <summary>The job already ended.</summary>
  NOT_ACTIVE,

  /// <summary>A waiting job was cancelled.</summary>
  CANCELLED,

  /// <summary>The running job will stop at its next check.</summary>
  REQUESTED
}

/// <summary>
///   Background jobs (re-track and validate), shared by every API replica and
///   the scheduler. Only one job runs at a time.
/// </summary>
public interface IJobService {
  /// <summary>
  ///   Starts a job now, unless another one is running.
  /// </summary>
  public Task<JobStart> TryStart(string kind, string? processor,
                                 string started_by);

  /// <summary>
  ///   Adds a scheduled job that waits until no other job runs.
  /// </summary>
  public Task<Job> AddWaiting(string kind, string? processor,
                              string started_by);

  /// <summary>
  ///   Turns a waiting job into the running one if nothing else runs.
  ///   Otherwise records what it waits for and returns false.
  /// </summary>
  public Task<bool> TryRun(Job job);

  /// <summary>
  ///   Saves the progress of a running job and returns what to do next.
  /// </summary>
  public Task<JobSignal> Report(Job job);

  /// <summary>
  ///   Ends a running job in <paramref name="state" />.
  /// </summary>
  public Task Finish(Job job, string state, string? message);

  /// <summary>
  ///   Cancels a waiting job, or asks a running one to stop.
  /// </summary>
  public Task<JobCancel> RequestCancel(string id, string user);

  /// <summary>The job, or null.</summary>
  public Task<Job?> Get(string id);

  /// <summary>The running job, or null.</summary>
  public Task<Job?> GetRunning();

  /// <summary>The newest jobs first.</summary>
  public Task<IReadOnlyList<Job>> List(int limit);
}
