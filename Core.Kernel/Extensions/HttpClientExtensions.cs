// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Kernel.Extensions;

/// <summary>
///   Extension methods for registering the HTTP clients that collectors use to
///   pull artifacts, and for bounding how long a single download may take.
/// </summary>
public static class HttpClientExtensions {
  /// <summary>
  ///   Registers a named <see cref="HttpClient" /> for artifact downloads.
  /// </summary>
  /// <remarks>
  ///   <see cref="HttpClient.Timeout" /> applies to the whole exchange,
  ///   including reading the response body, and
  ///   <see cref="HttpCompletionOption.ResponseHeadersRead" /> does not opt out
  ///   of it. Its 100 second default therefore caps every download at 100
  ///   seconds regardless of how large the artifact is, which no multi-gigabyte
  ///   model or image layer can meet. It is disabled here and replaced by an
  ///   explicit per-download budget applied by the caller through
  ///   <see cref="CreateDownloadTimeout" />, so a slow but progressing transfer
  ///   is allowed to finish while a stalled one is still abandoned.
  /// </remarks>
  /// <param name="services">The service collection.</param>
  /// <param name="name">The logical name of the client.</param>
  /// <param name="configure_client">Optional extra client configuration.</param>
  /// <param name="configure_handler">Optional extra handler configuration.</param>
  /// <returns>The HTTP client builder, for further configuration.</returns>
  public static IHttpClientBuilder AddDownloadClient(
    this IServiceCollection services, string name,
    Action<HttpClient>? configure_client = null,
    Action<SocketsHttpHandler>? configure_handler = null) {
    return services.AddHttpClient(
                     name,
                     client => {
                       client.Timeout = Timeout.InfiniteTimeSpan;
                       client.DefaultRequestHeaders.UserAgent
                             .ParseAdd("Backpack/1.0");
                       configure_client?.Invoke(client);
                     }
                   )
                   .ConfigurePrimaryHttpMessageHandler(
                     () => {
                       SocketsHttpHandler handler = new() {
                         /* The client no longer bounds the transfer, so the
                            connect phase needs its own limit: without it an
                            unreachable host holds a concurrency slot for the
                            entire download budget. */
                         ConnectTimeout = GetConnectTimeout(),
                         /* Artifacts are mirrored byte for byte. Transparent
                            decompression would store a .tgz served with
                            Content-Encoding: gzip in its decoded form, so the
                            stored bytes would no longer match the source. */
                         AutomaticDecompression = DecompressionMethods.None
                       };
                       configure_handler?.Invoke(handler);
                       return handler;
                     }
                   );
  }

  /// <summary>
  ///   Creates a cancellation source that trips once the configured download
  ///   budget is spent, while still honouring the caller's own cancellation.
  /// </summary>
  /// <param name="token">The token to link to, normally the consume context's.</param>
  /// <returns>A linked source the caller is responsible for disposing.</returns>
  public static CancellationTokenSource CreateDownloadTimeout(
    CancellationToken token) {
    CancellationTokenSource cts =
      CancellationTokenSource.CreateLinkedTokenSource(token);
    cts.CancelAfter(GetDownloadTimeout());
    return cts;
  }

  /// <summary>
  ///   Gets the wall-clock budget allowed for a single artifact download.
  /// </summary>
  /// <returns>The configured download timeout.</returns>
  public static TimeSpan GetDownloadTimeout() {
    return ParseTimeSpan(CoreVariables.BP_COLLECTOR_DOWNLOAD_TIMEOUT);
  }

  /// <summary>
  ///   Gets the time allowed to establish a connection to a remote host.
  /// </summary>
  /// <returns>The configured connect timeout.</returns>
  public static TimeSpan GetConnectTimeout() {
    return ParseTimeSpan(CoreVariables.BP_COLLECTOR_CONNECT_TIMEOUT);
  }

  /// <summary>
  ///   Reads a configuration variable as a <see cref="TimeSpan" />.
  /// </summary>
  /// <param name="variable">The variable to read.</param>
  /// <returns>The parsed value.</returns>
  /// <exception cref="FormatException">Thrown when the value is not a timespan.</exception>
  private static TimeSpan ParseTimeSpan(CoreVariables variable) {
    string value = Configuration.GetBackpackVariable(variable);
    /* Invariant culture on purpose: the value comes from an environment
       variable, so it must not be reinterpreted by the container's locale. */
    if (!TimeSpan.TryParse(
          value,
          CultureInfo.InvariantCulture,
          out TimeSpan parsed
        )) {
      throw new FormatException(
        $"{variable} is '{value}', which is not a valid timespan."
      );
    }

    return parsed;
  }
}
