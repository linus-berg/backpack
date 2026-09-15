// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Kernel.Health;

/// <summary>
///   Serves the registered health checks over HTTP so that an orchestrator can
///   probe a worker.
/// </summary>
/// <remarks>
///   The workers are console hosts published on the dotnet runtime image, which
///   does not carry the ASP.NET Core shared framework, so Kestrel and
///   MapHealthChecks are not available to them. A bare
///   <see cref="HttpListener" /> is enough for two routes and keeps the images
///   unchanged. Only the transport is bespoke: the checks themselves are
///   ordinary <see cref="IHealthCheck" /> registrations, so a later move to the
///   aspnet image is a matter of deleting this class.
/// </remarks>
public class HealthEndpoint : BackgroundService {
  /// <summary>
  ///   Tag marking a check that answers "is the process alive", as opposed to
  ///   "can the process do its job". Liveness must not depend on anything
  ///   external, or a broker outage restarts the whole fleet instead of just
  ///   pausing it.
  /// </summary>
  public const string C_LIVE_TAG = "live";

  /// <summary>
  ///   Tag marking a check that verifies a dependency the service needs before
  ///   it can accept work.
  /// </summary>
  public const string C_READY_TAG = "ready";

  /// <summary>
  ///   Time a single dependency check may take before it is called unhealthy.
  ///   A probe has to answer within the orchestrator's own timeout, so a
  ///   dependency that cannot acknowledge a ping this quickly is reported rather
  ///   than waited on.
  /// </summary>
  public static readonly TimeSpan S_DEPENDENCY_TIMEOUT =
    TimeSpan.FromSeconds(5);

  private readonly HealthCheckService health_;
  private readonly ILogger<HealthEndpoint> logger_;
  private readonly int port_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="HealthEndpoint" /> class.
  /// </summary>
  /// <param name="health">The health check service.</param>
  /// <param name="logger">The logger.</param>
  public HealthEndpoint(HealthCheckService health,
                        ILogger<HealthEndpoint> logger) {
    health_ = health;
    logger_ = logger;
    port_ = int.Parse(
      Configuration.GetBackpackVariable(CoreVariables.BP_HEALTH_PORT)
    );
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stopping_token) {
    using HttpListener listener = new();
    listener.Prefixes.Add($"http://+:{port_}/");

    try {
      listener.Start();
    } catch (Exception ex) {
      /* Losing the probe endpoint is bad, but killing a collector that is
         otherwise working is worse, so the failure is reported and the host
         keeps running. */
      logger_.LogError(
        ex,
        "Health endpoint could not bind port {Port}",
        port_
      );
      return;
    }

    logger_.LogInformation("Health endpoint listening on port {Port}", port_);
    /* GetContextAsync takes no token; closing the listener is what releases it. */
    using CancellationTokenRegistration registration =
      stopping_token.Register(listener.Close);

    try {
      while (!stopping_token.IsCancellationRequested) {
        HttpListenerContext context = await listener.GetContextAsync();
        /* Answered off the accept loop: a readiness check waiting on a
           dependency must not delay the liveness probe behind it. */
        _ = Task.Run(
          () => Respond(context, stopping_token),
          CancellationToken.None
        );
      }
    } catch (Exception) when (stopping_token.IsCancellationRequested) {
      /* Shutdown closed the listener out from under the pending accept. */
    } catch (Exception ex) {
      logger_.LogError(ex, "Health endpoint stopped accepting probes");
    }
  }

  /// <summary>
  ///   Runs the checks selected by the request path and writes the report.
  /// </summary>
  /// <param name="context">The listener context to answer.</param>
  /// <param name="token">The cancellation token.</param>
  private async Task Respond(HttpListenerContext context,
                             CancellationToken token) {
    try {
      string path =
        context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

      Func<HealthCheckRegistration, bool>? predicate;
      switch (path) {
        case "/health/live":
          predicate = r => r.Tags.Contains(C_LIVE_TAG);
          break;
        case "/health/ready":
          /* Everything that is not explicitly a liveness check counts towards
             readiness, so checks contributed by libraries participate whatever
             tags they happened to pick. */
          predicate = r => !r.Tags.Contains(C_LIVE_TAG);
          break;
        case "/health":
        case "":
          predicate = null;
          break;
        default:
          context.Response.StatusCode = (int)HttpStatusCode.NotFound;
          context.Response.Close();
          return;
      }

      HealthReport report = await health_.CheckHealthAsync(predicate, token);
      byte[] body =
        Encoding.UTF8.GetBytes(HealthReportJson.Serialize(report));

      context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
        ? (int)HttpStatusCode.ServiceUnavailable
        : (int)HttpStatusCode.OK;
      context.Response.ContentType = "application/json";
      context.Response.ContentLength64 = body.Length;
      await context.Response.OutputStream.WriteAsync(body, token);
      context.Response.Close();
    } catch (Exception ex) {
      logger_.LogWarning(ex, "Failed to answer a health probe");
      try {
        context.Response.Abort();
      } catch (Exception) {
        /* The probe already hung up. */
      }
    }
  }
}
