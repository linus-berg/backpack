// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.Kernel.Health;

/// <summary>
///   Results of the infrastructure checks.
/// </summary>
public static class HealthResults {
  /// <summary>
  ///   Key in <see cref="HealthCheckResult.Data" /> that marks a dependency
  ///   that is down, as opposed to one that works with a caveat (such as a
  ///   bucket that doesn't exist yet).
  /// </summary>
  public const string FAILED = "failed";

  private static readonly IReadOnlyDictionary<string, object> S_FAILED_ =
    new Dictionary<string, object> { [FAILED] = true };

  /// <summary>
  ///   A failed check, reported with the registration's failure status:
  ///   unhealthy where the service can't work without the dependency,
  ///   degraded where it only reports it. Either way it is marked
  ///   <see cref="FAILED" />, so the Status page shows it as down.
  /// </summary>
  /// <param name="context">The check's context.</param>
  /// <param name="description">What failed, for people.</param>
  /// <param name="exception">The cause, logged but never returned to clients.</param>
  public static HealthCheckResult Failed(HealthCheckContext context,
                                         string description,
                                         Exception? exception = null) {
    return new HealthCheckResult(
      context.Registration.FailureStatus,
      description,
      exception,
      S_FAILED_
    );
  }

  /// <summary>
  ///   True when a report entry is a <see cref="Failed" /> result.
  /// </summary>
  public static bool IsFailed(HealthReportEntry entry) {
    return entry.Data.TryGetValue(FAILED, out object? failed) && failed is true;
  }
}
