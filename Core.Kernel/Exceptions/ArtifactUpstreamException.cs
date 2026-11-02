// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Exceptions;

/// <summary>
///   The upstream source failed in a way that may pass: a network, DNS, TLS
///   or proxy error, a 5xx answer, or a download that ended early. Retried
///   immediately, then by delayed redelivery.
/// </summary>
public class ArtifactUpstreamException : ArtifactTransientException {
  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactUpstreamException" /> class.
  /// </summary>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="inner_exception">The cause, if any.</param>
  public ArtifactUpstreamException(string? message,
                                   Exception? inner_exception = null) :
    base(message, inner_exception) {
  }
}
