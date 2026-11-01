// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Core.Kernel.Models;

/// <summary>
///   Kinds of background job.
/// </summary>
public static class JobKinds {
  /// <summary>
  ///   Re-tracks root artifacts: asks the processors again for new versions
  ///   and dependencies.
  /// </summary>
  public const string TRACK = "track";

  /// <summary>
  ///   Validates artifacts: sends the versions Backpack knows to the
  ///   collectors again, so missing files are downloaded.
  /// </summary>
  public const string VALIDATE = "validate";

  /// <summary>
  ///   True for a kind Backpack knows.
  /// </summary>
  public static bool IsKnown(string? kind) {
    return kind is TRACK or VALIDATE;
  }
}

/// <summary>
///   States of a background job.
/// </summary>
public static class JobStates {
  /// <summary>A scheduled run waiting for the running job to finish.</summary>
  public const string WAITING = "waiting";

  /// <summary>Running.</summary>
  public const string RUNNING = "running";

  /// <summary>Every artifact was handled (some may have failed).</summary>
  public const string SUCCEEDED = "succeeded";

  /// <summary>Stopped by an error before it got through every artifact.</summary>
  public const string FAILED = "failed";

  /// <summary>Cancelled by an administrator.</summary>
  public const string CANCELLED = "cancelled";

  /// <summary>
  ///   The service running it stopped, or stopped responding.
  /// </summary>
  public const string INTERRUPTED = "interrupted";

  /// <summary>A scheduled run that waited too long for another job.</summary>
  public const string SKIPPED = "skipped";

  /// <summary>
  ///   True while a job is waiting or running.
  /// </summary>
  public static bool IsActive(string? state) {
    return state is WAITING or RUNNING;
  }
}

/// <summary>
///   A background job over many artifacts, such as "validate every artifact"
///   or "re-track npm". Stored in MongoDB (<c>backpack-jobs</c>), so every
///   API replica and the scheduler see the same jobs, and only one runs at a
///   time.
/// </summary>
public class Job {
  /// <summary>Job id.</summary>
  public string id { get; set; } = Guid.NewGuid().ToString("N");

  /// <summary>A <see cref="JobKinds" /> value.</summary>
  public required string kind { get; set; }

  /// <summary>The processor it covers, or null for every processor.</summary>
  public string? processor { get; set; }

  /// <summary>A <see cref="JobStates" /> value.</summary>
  public string state { get; set; } = JobStates.RUNNING;

  /// <summary>Who started it: a user name, or "Scheduler".</summary>
  public required string started_by { get; set; }

  /// <summary>True when a schedule started it.</summary>
  public bool scheduled { get; set; }

  /// <summary>When it was created (UTC).</summary>
  public DateTime created_at { get; set; } = DateTime.UtcNow;

  /// <summary>When it started running (UTC).</summary>
  public DateTime? started_at { get; set; }

  /// <summary>When it ended (UTC).</summary>
  public DateTime? finished_at { get; set; }

  /// <summary>
  ///   Last sign of life from the service running or waiting for it (UTC).
  /// </summary>
  public DateTime heartbeat_at { get; set; } = DateTime.UtcNow;

  /// <summary>The host (pod) running it.</summary>
  public string? host { get; set; }

  /// <summary>Artifacts to handle, counted when it starts.</summary>
  public long total { get; set; }

  /// <summary>Artifacts handled so far, including failed ones.</summary>
  public long done { get; set; }

  /// <summary>Artifacts that could not be handled.</summary>
  public long failed { get; set; }

  /// <summary>The processor it is working through.</summary>
  public string? current_processor { get; set; }

  /// <summary>The latest problem, or a summary once it ends.</summary>
  public string? message { get; set; }

  /// <summary>An administrator asked to cancel it.</summary>
  public bool cancel_requested { get; set; }

  /// <summary>Who asked to cancel it.</summary>
  public string? cancelled_by { get; set; }

  /// <summary>
  ///   Set while running. A unique index on it lets only one job run.
  /// </summary>
  [JsonIgnore]
  public string? lock_key { get; set; }
}
