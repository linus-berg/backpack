// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using Collector.Kernel;
using Core.Kernel.Exceptions;

namespace Collector.Http;

/// <summary>
///   Represents a remote file accessible via HTTP.
/// </summary>
public class RemoteFile {
  private readonly HttpClient client_;
  private readonly FileSystem fs_;
  private readonly string url_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RemoteFile" /> class.
  /// </summary>
  /// <param name="client">The HTTP client to use for requests.</param>
  /// <param name="url">The URL of the remote file.</param>
  /// <param name="fs">The file system.</param>
  public RemoteFile(HttpClient client, string url, FileSystem fs) {
    client_ = client;
    url_ = url;
    fs_ = fs;
  }

  /// <summary>
  ///   Fetches the remote file and stores it at <paramref name="path" />.
  ///   Failures that may pass are thrown, so the request is retried; a
  ///   refusal that won't change is returned.
  /// </summary>
  /// <param name="path">The storage key.</param>
  /// <param name="token">The cancellation token.</param>
  /// <returns>
  ///   Whether the file was stored, the response status and the size the
  ///   server announced (if any).
  /// </returns>
  /// <exception cref="ArtifactRateLimitedException">
  ///   HTTP 429, or 503 with Retry-After: retried later by delayed
  ///   redelivery only.
  /// </exception>
  /// <exception cref="ArtifactUpstreamException">
  ///   A network, DNS, TLS or proxy error, HTTP 425 or 5xx, or a body that
  ///   ended early: retried.
  /// </exception>
  /// <exception cref="ArtifactTimeoutException">
  ///   No response in time, or HTTP 408: retried.
  /// </exception>
  /// <exception cref="ArtifactStorageException">
  ///   S3 or the temp disk failed (from <see cref="FileSystem" />).
  /// </exception>
  public async Task<Download> Get(string path, CancellationToken token = default) {
    HttpResponseMessage response;
    try {
      response = await client_.GetAsync(
                   url_,
                   HttpCompletionOption.ResponseHeadersRead,
                   token
                 );
    } catch (OperationCanceledException) when (token.IsCancellationRequested) {
      throw;
    } catch (TaskCanceledException e) {
      throw new ArtifactTimeoutException(
        $"{url_}: no response within {client_.Timeout.TotalSeconds:N0} s",
        e
      );
    } catch (HttpRequestException e) {
      /* DNS, connection, TLS and proxy errors. */
      throw new ArtifactUpstreamException($"{url_}: {e.Message}", e);
    }

    using (response) {
      int status = (int)response.StatusCode;
      if (!response.IsSuccessStatusCode) {
        string answer = $"{url_} answered {status} {response.ReasonPhrase}";
        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            (response.StatusCode == HttpStatusCode.ServiceUnavailable &&
             response.Headers.RetryAfter != null)) {
          throw new ArtifactRateLimitedException(
            $"{answer}{RetryAfter(response)}; retrying later"
          );
        }

        if (status == 408) {
          throw new ArtifactTimeoutException($"{answer}; retrying");
        }

        if (status == 425 || status >= 500) {
          throw new ArtifactUpstreamException($"{answer}; retrying");
        }

        /* 4xx (and redirects that were not followed, such as HTTPS to
           HTTP) won't fix themselves. */
        return new Download(false, response.StatusCode, Redirect(response));
      }

      long? size = response.Content.Headers.ContentLength;
      try {
        /* Nothing is deleted on failure. PutFile buffers the whole body
           before it uploads and an S3 put is all or nothing, so a failed
           collect never leaves a partial object at path. Whatever is there
           is either the previous good copy or a concurrent collect's
           finished upload. */
        Stream body = await response.Content.ReadAsStreamAsync(token);
        await fs_.PutFile(path, body);
      } catch (OperationCanceledException) when (token.IsCancellationRequested) {
        throw;
      } catch (TaskCanceledException e) {
        throw new ArtifactTimeoutException($"{url_}: the download timed out", e);
      } catch (Exception e)
        when (e is IOException or HttpRequestException or SocketException) {
        /* FileSystem reports its own failures as ArtifactStorageException,
           so these come from reading the body. */
        throw new ArtifactUpstreamException(
          $"{url_}: the download ended early ({e.Message}); retrying",
          e
        );
      }

      return new Download(true, response.StatusCode, null, size);
    }
  }

  private static string RetryAfter(HttpResponseMessage response) {
    TimeSpan? delay = response.Headers.RetryAfter?.Delta ??
                      response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow;
    return delay is { } d && d > TimeSpan.Zero
             ? $" (Retry-After: {Math.Ceiling(d.TotalSeconds):N0} s)"
             : "";
  }

  private static string? Redirect(HttpResponseMessage response) {
    return (int)response.StatusCode is >= 300 and < 400 &&
           response.Headers.Location is { } location
             ? $"redirect to {location} was not followed"
             : null;
  }

  /// <summary>
  ///   The outcome of a download that did not throw.
  /// </summary>
  /// <param name="stored">The file is stored.</param>
  /// <param name="status">The response status.</param>
  /// <param name="detail">Why it was not stored, beyond the status.</param>
  /// <param name="size">The size the server announced, if any.</param>
  public sealed record Download(bool stored,
                                HttpStatusCode status,
                                string? detail,
                                long? size = null);
}
