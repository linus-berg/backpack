// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Kernel.Health;

/// <summary>
///   Health endpoint of worker services, which have no web server of their
///   own: a minimal HTTP/1.1 responder on <see cref="CoreVariables.BP_HEALTH_PORT" />
///   for Kubernetes probes, listening on IPv4 and IPv6.
///   <list type="bullet">
///     <item>
///       <c>GET /healthz/live</c>: 200 while the service is running, 503
///       while it starts or stops. Checks no dependencies, so a dependency
///       outage doesn't restart every pod.
///     </item>
///     <item>
///       <c>GET /healthz/ready</c>: runs the checks tagged
///       <see cref="HealthTags.READY" /> (RabbitMQ, and MongoDB, Redis or
///       S3 where used). 503 when a check is unhealthy or the MassTransit bus
///       isn't fully healthy, otherwise 200 (a degraded check, such as a
///       bucket that doesn't exist yet, still counts as ready).
///     </item>
///   </list>
///   Every connection gets one answer and is closed.
/// </summary>
public sealed class HealthEndpointService : BackgroundService {
  /* Probes send a request line and a few headers. */
  private const int C_MAX_HEAD_ = 8 * 1024;

  /* Probes are rare; anything beyond this is turned away. */
  private const int C_MAX_CONNECTIONS_ = 16;

  /* Reading the request plus the readiness checks (3 seconds each, in parallel). */
  private static readonly TimeSpan S_REQUEST_TIMEOUT_ = TimeSpan.FromSeconds(10);

  private readonly HealthCheckService health_;
  private readonly IHostApplicationLifetime lifetime_;
  private readonly ILogger<HealthEndpointService> logger_;
  private readonly SemaphoreSlim slots_ = new(C_MAX_CONNECTIONS_);

  /// <summary>
  ///   Initializes a new instance of the <see cref="HealthEndpointService" /> class.
  /// </summary>
  /// <param name="health">Runs the registered health checks.</param>
  /// <param name="lifetime">Tells whether the service has started.</param>
  /// <param name="logger">Logger.</param>
  public HealthEndpointService(HealthCheckService health,
                               IHostApplicationLifetime lifetime,
                               ILogger<HealthEndpointService> logger) {
    health_ = health;
    lifetime_ = lifetime;
    logger_ = logger;
  }

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stopping_token) {
    string value = Configuration.GetBackpackVariable(CoreVariables.BP_HEALTH_PORT);
    if (string.IsNullOrWhiteSpace(value)) {
      logger_.LogInformation("Health endpoint disabled: BP_HEALTH_PORT is empty");
      return;
    }

    if (!int.TryParse(value, out int port) || port is < 1 or > 65535) {
      logger_.LogWarning(
        "Health endpoint disabled: BP_HEALTH_PORT {Port} is not a port",
        value
      );
      return;
    }

    TcpListener? listener = Listen(port);
    if (listener == null) {
      return;
    }

    logger_.LogInformation(
      "Health endpoint on port {Port}: /healthz/live, /healthz/ready",
      port
    );
    try {
      while (!stopping_token.IsCancellationRequested) {
        TcpClient client;
        try {
          client = await listener.AcceptTcpClientAsync(stopping_token);
        } catch (OperationCanceledException) {
          break;
        } catch (SocketException e) {
          logger_.LogDebug("Health endpoint: {Error}", e.Message);
          continue;
        }

        if (!slots_.Wait(0)) {
          client.Dispose();
          continue;
        }

        _ = Task.Run(() => Serve(client, stopping_token), CancellationToken.None);
      }
    } finally {
      listener.Stop();
    }
  }

  /// <summary>
  ///   Listens on every IPv6 and IPv4 address, or only IPv4 where the
  ///   kernel has no IPv6. Null when the port is taken, e.g. by another
  ///   service on the same machine during development: the service works
  ///   without it, only probes can't reach it.
  /// </summary>
  private TcpListener? Listen(int port) {
    SocketException? error = null;
    foreach (bool dual in new[] { true, false }) {
      if (dual && !Socket.OSSupportsIPv6) {
        continue;
      }

      TcpListener listener = new(dual ? IPAddress.IPv6Any : IPAddress.Any, port);
      try {
        if (dual) {
          listener.Server.DualMode = true;
        }

        listener.Start();
        return listener;
      } catch (SocketException e) {
        listener.Stop();
        error = e;
        if (e.SocketErrorCode == SocketError.AddressAlreadyInUse) {
          break;
        }
      }
    }

    logger_.LogWarning(
      "Health endpoint not started on port {Port}: {Error}",
      port,
      error?.Message
    );
    return null;
  }

  private bool IsRunning() {
    return lifetime_.ApplicationStarted.IsCancellationRequested &&
           !lifetime_.ApplicationStopping.IsCancellationRequested;
  }

  private async Task Serve(TcpClient client, CancellationToken stopping_token) {
    try {
      using (client) {
        using CancellationTokenSource timeout =
          CancellationTokenSource.CreateLinkedTokenSource(stopping_token);
        timeout.CancelAfter(S_REQUEST_TIMEOUT_);
        NetworkStream stream = client.GetStream();
        string? request_line = await ReadRequestLine(stream, timeout.Token);
        string[] parts = request_line?.Split(' ') ?? Array.Empty<string>();
        if (parts.Length != 3 || !parts[2].StartsWith("HTTP/")) {
          await Write(stream, 400, "{}", false, timeout.Token);
          return;
        }

        string method = parts[0];
        bool head = method == "HEAD";
        if (method != "GET" && !head) {
          await Write(stream, 405, "{}", false, timeout.Token);
          return;
        }

        string path = parts[1].Split('?', 2)[0].TrimEnd('/');
        (int status, string body) = path switch {
          "/healthz/live" => Live(),
          "/healthz/ready" => await Ready(timeout.Token),
          _ => (404, "{}")
        };
        await Write(stream, status, body, head, timeout.Token);
      }
    } catch (Exception e) when (e is OperationCanceledException or IOException
                                  or SocketException) {
      // The client went away or took too long.
    } catch (Exception e) {
      logger_.LogWarning(e, "Health endpoint failed to answer");
    } finally {
      slots_.Release();
    }
  }

  private (int, string) Live() {
    bool running = IsRunning();
    return (running ? 200 : 503,
            HealthReportJson.Serialize(
              running ? HealthStatus.Healthy : HealthStatus.Unhealthy
            ));
  }

  private async Task<(int, string)> Ready(CancellationToken token) {
    if (!IsRunning()) {
      return (503, HealthReportJson.Serialize(HealthStatus.Unhealthy));
    }

    HealthReport report = await health_.CheckHealthAsync(
                            check => check.Tags.Contains(HealthTags.READY),
                            token
                          );
    /* A worker exists to consume queues. MassTransit reports a bus that is
       reconnecting to RabbitMQ as degraded, which for a worker means it
       isn't doing its job. HealthCheckService already logs failing checks. */
    bool consuming = report.Entries.Values
                           .Where(e => e.Tags.Contains(HealthTags.MASSTRANSIT))
                           .All(e => e.Status == HealthStatus.Healthy);
    bool ready = consuming && report.Status != HealthStatus.Unhealthy;
    return (ready ? 200 : 503,
            HealthReportJson.Serialize(report, ready ? null : HealthStatus.Unhealthy));
  }

  /// <summary>
  ///   Reads the request up to the blank line that ends its headers and
  ///   returns the request line, or null if the head is incomplete or too
  ///   long. Probes send no body.
  /// </summary>
  private static async Task<string?> ReadRequestLine(NetworkStream stream,
                                                     CancellationToken token) {
    byte[] buffer = new byte[C_MAX_HEAD_];
    int length = 0;
    while (length < buffer.Length) {
      int read = await stream.ReadAsync(buffer.AsMemory(length), token);
      if (read == 0) {
        return null;
      }

      length += read;
      if (buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8) >= 0) {
        int end = buffer.AsSpan(0, length).IndexOf("\r\n"u8);
        return Encoding.ASCII.GetString(buffer, 0, end);
      }
    }

    return null;
  }

  private static async Task Write(NetworkStream stream,
                                  int status,
                                  string body,
                                  bool head,
                                  CancellationToken token) {
    string reason = status switch {
      200 => "OK",
      400 => "Bad Request",
      404 => "Not Found",
      405 => "Method Not Allowed",
      _ => "Service Unavailable"
    };
    byte[] content = Encoding.UTF8.GetBytes(body);
    string headers = $"HTTP/1.1 {status} {reason}\r\n" +
                     "Content-Type: application/json\r\n" +
                     $"Content-Length: {content.Length}\r\n" +
                     "Cache-Control: no-store\r\n" +
                     (status == 405 ? "Allow: GET, HEAD\r\n" : "") +
                     "Connection: close\r\n\r\n";
    await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), token);
    if (!head) {
      await stream.WriteAsync(content, token);
    }
  }
}
