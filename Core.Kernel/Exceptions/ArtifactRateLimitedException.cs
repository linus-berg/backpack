// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Exceptions;

/// <summary>
///   The upstream server asked us to slow down (HTTP 429, or 503 with
///   Retry-After). Retried only by delayed redelivery: retrying at once would
///   only hit the rate limit again.
/// </summary>
public class ArtifactRateLimitedException : ArtifactTransientException {
  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactRateLimitedException" /> class.
  /// </summary>
  /// <param name="message">The message that describes the error.</param>
  public ArtifactRateLimitedException(string? message) : base(message) {
  }
}
