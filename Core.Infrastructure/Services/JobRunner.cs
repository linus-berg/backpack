using System.Diagnostics;
using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Services;

/// <summary>
///   Runs a re-track or validation job: streams the artifacts of each
///   processor from MongoDB and sends each one on (to the gateway for a
///   re-track, to the collectors for a validation), saving progress as it
///   goes. The job ends once everything is sent; the processors and
///   collectors then work through their queues.
/// </summary>
public sealed class JobRunner {
  /* How often progress is saved while artifacts are being sent. */
  private static readonly TimeSpan S_REPORT_EVERY_ = TimeSpan.FromSeconds(1);

  /* Heartbeats keep the job alive while a single send is slow. */
  private static readonly TimeSpan S_HEARTBEAT_EVERY_ = TimeSpan.FromSeconds(10);

  private readonly IArtifactService artifacts_;
  private readonly ICoreDatabase db_;
  private readonly IEventService events_;
  private readonly IJobService jobs_;
  private readonly ILogger<JobRunner> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="JobRunner" /> class.
  /// </summary>
  public JobRunner(IJobService jobs, IArtifactService artifacts,
                   ICoreDatabase db, IEventService events,
                   ILogger<JobRunner> logger) {
    jobs_ = jobs;
    artifacts_ = artifacts;
    db_ = db;
    events_ = events;
    logger_ = logger;
  }

  /// <summary>
  ///   Runs a job that is in the running state.
  /// </summary>
  /// <param name="job">The job.</param>
  /// <param name="stopping">Cancelled when the service shuts down.</param>
  /// <returns>The state it ended in.</returns>
  public async Task<string> Run(Job job, CancellationToken stopping) {
    using CancellationTokenSource stop =
      CancellationTokenSource.CreateLinkedTokenSource(stopping);
    JobSignal signal = JobSignal.CONTINUE;

    void Handle(JobSignal next) {
      if (next != JobSignal.CONTINUE && signal == JobSignal.CONTINUE) {
        signal = next;
        stop.Cancel();
      }
    }

    Task heartbeat = Heartbeat(job, Handle, stop.Token);
    Stopwatch clock = Stopwatch.StartNew();
    string state;
    string? message;
    try {
      bool roots_only = job.kind == JobKinds.TRACK;
      List<Processor> processors = await GetProcessors(job);
      foreach (Processor processor in processors) {
        job.total += await db_.CountArtifacts(processor.id, roots_only);
      }

      Handle(await jobs_.Report(job));
      logger_.LogInformation(
        "Job {Job}: {Kind} of {Total} artifacts in {Processors} processors",
        job.id,
        job.kind,
        job.total,
        processors.Count
      );
      await LogEvent(job, $"{Title(job)} started", Started(job));

      TimeSpan last_report = TimeSpan.Zero;
      foreach (Processor processor in processors) {
        job.current_processor = processor.id;
        await foreach (Artifact artifact in db_.StreamArtifacts(
                         processor.id,
                         roots_only,
                         stop.Token
                       )) {
          try {
            if (job.kind == JobKinds.TRACK) {
              await artifacts_.Track(artifact, processor);
            } else {
              await artifacts_.Validate(artifact, processor);
            }
          } catch (Exception e) when (e is not OperationCanceledException) {
            /* One broken artifact must not stop the rest. */
            job.failed++;
            job.message = $"{processor.id}/{artifact.id}: {e.Message}";
            logger_.LogError(
              e,
              "Job {Job}: {Processor}/{Artifact} failed",
              job.id,
              processor.id,
              artifact.id
            );
          }

          job.done++;
          if (clock.Elapsed - last_report >= S_REPORT_EVERY_) {
            last_report = clock.Elapsed;
            Handle(await jobs_.Report(job));
          }

          stop.Token.ThrowIfCancellationRequested();
        }
      }

      state = JobStates.SUCCEEDED;
      message =
        $"Sent {job.done:N0} artifacts in {Duration(clock.Elapsed)}" +
        (job.failed > 0 ? $"; {job.failed:N0} failed (last: {job.message})." : ".");
    } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
      (state, message) = signal switch {
        JobSignal.CANCEL => (JobStates.CANCELLED,
                             $"Cancelled by {job.cancelled_by ?? "an administrator"} after {job.done:N0} of {job.total:N0} artifacts."),
        JobSignal.LOST => (JobStates.INTERRUPTED, null),
        _ => (JobStates.INTERRUPTED,
              $"The service running it stopped after {job.done:N0} of {job.total:N0} artifacts. Start it again.")
      };
    } catch (Exception e) {
      logger_.LogError(e, "Job {Job} failed", job.id);
      state = JobStates.FAILED;
      message = $"Stopped after {job.done:N0} of {job.total:N0} artifacts: {e.Message}";
    } finally {
      stop.Cancel();
      await heartbeat;
    }

    if (signal == JobSignal.LOST) {
      /* Another service already marked it interrupted. */
      logger_.LogWarning("Job {Job} was taken over while running", job.id);
      return JobStates.INTERRUPTED;
    }

    await jobs_.Finish(job, state, message);
    await LogEvent(
      job,
      $"{Title(job)} {StateWord(state)}: {message}",
      state == JobStates.SUCCEEDED && job.failed == 0
        ? EventSeverity.SUCCESS
        : state == JobStates.SUCCEEDED || state == JobStates.CANCELLED
          ? EventSeverity.WARNING
          : EventSeverity.ERROR
    );
    return state;
  }

  /// <summary>
  ///   "Validate all artifacts", "Re-track npm".
  /// </summary>
  public static string Title(Job job) {
    string verb = job.kind == JobKinds.VALIDATE ? "Validate" : "Re-track";
    return job.processor == null ? $"{verb} all artifacts" : $"{verb} {job.processor}";
  }

  private async Task<List<Processor>> GetProcessors(Job job) {
    List<Processor> processors = (await db_.GetProcessors()).ToList();
    if (job.processor == null) {
      return processors;
    }

    Processor? processor = processors.FirstOrDefault(p => p.id == job.processor);
    if (processor == null) {
      throw new InvalidOperationException(
        $"There is no processor {job.processor}."
      );
    }

    return new List<Processor> { processor };
  }

  private async Task Heartbeat(Job job, Action<JobSignal> handle,
                               CancellationToken token) {
    using PeriodicTimer timer = new(S_HEARTBEAT_EVERY_);
    try {
      while (await timer.WaitForNextTickAsync(token)) {
        try {
          handle(await jobs_.Report(job));
        } catch (Exception e) {
          /* MongoDB may be briefly unavailable; the stale check gives it
             two minutes before the job counts as interrupted. */
          logger_.LogWarning("Job {Job}: heartbeat failed: {Error}", job.id, e.Message);
        }
      }
    } catch (OperationCanceledException) {
      // The job ended.
    }
  }

  private async Task LogEvent(Job job, string message, EventSeverity severity) {
    try {
      await events_.LogEvent("Jobs", message, severity, job.started_by);
    } catch (Exception e) {
      logger_.LogWarning("Job {Job}: could not log an event: {Error}", job.id, e.Message);
    }
  }

  private static EventSeverity Started(Job job) {
    return job.processor == null ? EventSeverity.WARNING : EventSeverity.INFO;
  }

  private static string StateWord(string state) {
    return state switch {
      JobStates.SUCCEEDED => "finished",
      JobStates.CANCELLED => "cancelled",
      JobStates.FAILED => "failed",
      _ => "interrupted"
    };
  }

  private static string Duration(TimeSpan elapsed) {
    return elapsed.TotalMinutes >= 1
             ? $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds} s"
             : $"{Math.Max(1, (int)Math.Round(elapsed.TotalSeconds))} s";
  }
}
