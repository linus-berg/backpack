// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Exceptions;
using Core.Kernel.Messages;
using MassTransit;

namespace Collector.Http;

/// <summary>
///   Consumer for HTTP artifact collection requests. Every outcome is logged:
///   failures that may pass are thrown as an <see cref="ArtifactTransientException" />
///   that says what failed (upstream, timeout, rate limit, storage), so the
///   request is retried the right way; refusals that won't change (404 and
///   other 4xx) are logged as warnings and the request ends.
/// </summary>
public class Consumer : ICollector {
  private readonly bool delta_;
  private readonly bool forward_;
  private readonly FileSystem fs_;
  private readonly IHttpClientFactory http_client_factory_;
  private readonly ILogger<Consumer> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Consumer" /> class.
  /// </summary>
  /// <param name="fs">The file system.</param>
  /// <param name="http_client_factory">The HTTP client factory.</param>
  /// <param name="logger">The logger.</param>
  public Consumer(FileSystem fs, IHttpClientFactory http_client_factory,
                  ILogger<Consumer> logger) {
    fs_ = fs;
    http_client_factory_ = http_client_factory;
    logger_ = logger;
    delta_ =
      Configuration.GetBackpackVariable(
        CoreVariables.BP_COLLECTOR_HTTP_DELTA
      ) ==
      "true";
    forward_ =
      Configuration.GetBackpackVariable(CoreVariables.BP_COLLECTOR_HTTP_MODE) ==
      "forward";
  }

  /// <inheritdoc />
  public async Task Consume(ConsumeContext<ArtifactCollectRequest> context) {
    string location = context.Message.location;
    string module = context.Message.module;
    string fp;
    try {
      fp = fs_.GetArtifactPath(module, location);
    } catch (UriFormatException e) {
      logger_.LogWarning(
        "Not collected: {Location} is not a valid URL ({Error})",
        location,
        e.Message
      );
      return;
    }

    bool exists = await fs_.Exists(fp);
    if (exists && !context.Message.force) {
      /* A previous attempt may have stored the file and then failed to
         record its delta link; without the link it would never be
         transferred. */
      if (delta_ &&
          (context.GetRetryAttempt() > 0 || context.GetRedeliveryCount() > 0)) {
        await LinkDelta(module, location, fp);
      }

      logger_.LogDebug("Skipped {Location}: {Path} is already stored", location, fp);
      return;
    }

    Stopwatch clock = Stopwatch.StartNew();
    using HttpClient client = http_client_factory_.CreateClient("fetch-client");
    RemoteFile.Download download =
      await new RemoteFile(client, location, fs_).Get(fp, context.CancellationToken);
    if (!download.stored) {
      logger_.LogWarning(
        "Not collected: {Location} answered {Status} {StatusCode}{Detail}. Not retried; check the URL, or the credentials and proxy if access was refused",
        location,
        (int)download.status,
        download.status,
        download.detail == null ? "" : $" ({download.detail})"
      );
      return;
    }

    logger_.LogInformation(
      "Collected {Location} to {Path} ({Size} in {Elapsed} ms)",
      location,
      fp,
      download.size is { } size ? $"{size:N0} bytes" : "size unknown",
      clock.ElapsedMilliseconds
    );
    if (delta_) {
      await LinkDelta(module, location, fp);
    }
  }

  /// <summary>
  ///   Records the delta link of a stored file, so that the next transfer
  ///   includes it. Throws <see cref="ArtifactStorageException" /> when it
  ///   can't, so the request is retried and the file is not left out of
  ///   transfers.
  /// </summary>
  private async Task LinkDelta(string module, string location, string fp) {
    /* The storage pipeline retries the upload, so the artifact should be in
       place by now. It is confirmed anyway, because a link to a key that is
       not in the bucket is worse than no link at all. */
    if (!await fs_.Exists(fp)) {
      throw new ArtifactStorageException(
        $"Collected {location} but {fp} is not in the bucket; retrying"
      );
    }

    if (!await fs_.CreateDeltaLink(module, location, fp)) {
      throw new ArtifactStorageException(
        $"Collected {location} but could not record the delta link of {fp}; retrying"
      );
    }
  }
}
