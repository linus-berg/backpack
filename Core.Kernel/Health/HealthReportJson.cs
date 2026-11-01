// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.Kernel.Health;

/// <summary>
///   The JSON body of the health endpoints. It carries each check's status
///   and description, never exception details: the API's endpoint is
///   anonymous, and exceptions can name internal hosts.
/// </summary>
public static class HealthReportJson {
  /// <summary>
  ///   Serializes a health report.
  /// </summary>
  /// <param name="report">The report.</param>
  /// <param name="status">
  ///   The overall status to report, if stricter than the report's own.
  /// </param>
  /// <returns>
  ///   <c>{ status, duration_ms, checks: [{ name, status, description, duration_ms }] }</c>
  /// </returns>
  public static string Serialize(HealthReport report,
                                 HealthStatus? status = null) {
    return JsonSerializer.Serialize(
      new {
        status = (status ?? report.Status).ToString(),
        duration_ms = Math.Round(report.TotalDuration.TotalMilliseconds),
        checks = report.Entries.Select(
          entry => new {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration_ms = Math.Round(entry.Value.Duration.TotalMilliseconds)
          }
        )
      }
    );
  }

  /// <summary>
  ///   Serializes a status without checks, e.g. for liveness.
  /// </summary>
  /// <param name="status">The status.</param>
  /// <returns><c>{ status }</c></returns>
  public static string Serialize(HealthStatus status) {
    return JsonSerializer.Serialize(new { status = status.ToString() });
  }
}
