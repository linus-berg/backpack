using Core.Kernel.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Integration.API.Health;

/// <summary>
///   Health of one piece of infrastructure.
/// </summary>
/// <param name="name">
///   Check name: rabbitmq, rabbitmq-management, mongodb, redis, s3 or oidc.
/// </param>
/// <param name="status">
///   Healthy, Degraded (works with a caveat) or Unhealthy (down). A dependency
///   that is down is Unhealthy here even where it only degrades the API's
///   readiness.
/// </param>
/// <param name="description">What the check found.</param>
/// <param name="duration_ms">How long the check took.</param>
public sealed record InfrastructureCheck(
  string name,
  string status,
  string? description,
  double duration_ms);

/// <summary>
///   Health of the infrastructure, as the API sees it.
/// </summary>
/// <param name="status">The worst status of the checks.</param>
/// <param name="checked_at">When the checks ran (UTC).</param>
/// <param name="checks">One entry per check.</param>
public sealed record InfrastructureHealth(
  string status,
  DateTime checked_at,
  IReadOnlyList<InfrastructureCheck> checks);

/// <summary>
///   Runs the checks tagged <see cref="HealthTags.INFRASTRUCTURE" /> for the
///   Status page. Results are shared for 10 seconds, so any number of open
///   Status pages cost one round of checks per 10 seconds.
/// </summary>
public sealed class InfrastructureHealthService {
  private static readonly TimeSpan S_MAX_AGE_ = TimeSpan.FromSeconds(10);

  private readonly HealthCheckService health_;
  private readonly SemaphoreSlim lock_ = new(1, 1);
  private InfrastructureHealth? last_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="InfrastructureHealthService" /> class.
  /// </summary>
  /// <param name="health">Runs the registered health checks.</param>
  public InfrastructureHealthService(HealthCheckService health) {
    health_ = health;
  }

  /// <summary>
  ///   The latest results, running the checks if they are older than 10
  ///   seconds.
  /// </summary>
  /// <param name="token">Stops waiting; the checks themselves finish.</param>
  public async Task<InfrastructureHealth> GetAsync(CancellationToken token) {
    if (IsFresh(last_)) {
      return last_!;
    }

    await lock_.WaitAsync(token);
    try {
      if (IsFresh(last_)) {
        return last_!;
      }

      /* Not cancelled with the request: other callers share the result,
         and every check has its own timeout. */
      HealthReport report = await health_.CheckHealthAsync(
                              check => check.Tags.Contains(HealthTags.INFRASTRUCTURE),
                              CancellationToken.None
                            );
      List<(string name, HealthStatus status, HealthReportEntry entry)> checks =
        report.Entries
              .Select(
                e => (e.Key,
                      HealthResults.IsFailed(e.Value)
                        ? HealthStatus.Unhealthy
                        : e.Value.Status, e.Value)
              )
              .ToList();
      HealthStatus worst = checks.Count == 0
                             ? HealthStatus.Healthy
                             : checks.Min(c => c.status);
      last_ = new InfrastructureHealth(
        worst.ToString(),
        DateTime.UtcNow,
        checks.Select(
                c => new InfrastructureCheck(
                  c.name,
                  c.status.ToString(),
                  c.entry.Description,
                  Math.Round(c.entry.Duration.TotalMilliseconds)
                )
              )
              .ToList()
      );
      return last_;
    } finally {
      lock_.Release();
    }
  }

  private static bool IsFresh(InfrastructureHealth? health) {
    return health != null && DateTime.UtcNow - health.checked_at < S_MAX_AGE_;
  }
}
