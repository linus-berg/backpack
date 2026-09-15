// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Core.Kernel.Health;

/// <summary>
///   Renders a <see cref="HealthReport" /> as JSON.
/// </summary>
/// <remarks>
///   Shared by the worker health endpoint and the API, so that every service in
///   the fleet answers a probe with the same document shape no matter which
///   transport is serving it.
/// </remarks>
public static class HealthReportJson {
  private static readonly JsonSerializerOptions S_OPTIONS_ = new() {
    WriteIndented = false
  };

  /// <summary>
  ///   Serializes a health report.
  /// </summary>
  /// <param name="report">The report to serialize.</param>
  /// <returns>The report as a JSON document.</returns>
  public static string Serialize(HealthReport report) {
    Dictionary<string, object?> payload = new() {
      ["status"] = report.Status.ToString(),
      ["duration_ms"] = (long)report.TotalDuration.TotalMilliseconds,
      ["checks"] = report.Entries.ToDictionary(
        entry => entry.Key,
        entry => (object?)new Dictionary<string, object?> {
          ["status"] = entry.Value.Status.ToString(),
          ["description"] = entry.Value.Description,
          /* The message only, never the stack trace: the endpoint is reachable
             by anything that can reach the pod. */
          ["error"] = entry.Value.Exception?.Message,
          ["duration_ms"] = (long)entry.Value.Duration.TotalMilliseconds
        }
      )
    };
    return JsonSerializer.Serialize(payload, S_OPTIONS_);
  }
}
