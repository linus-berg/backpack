// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Exceptions;

/// <summary>
///   A failure that may pass, so the message is retried (see
///   <c>RegistrationUtils.ConfigureRetrying</c>). Throw one of the subtypes,
///   which say what failed:
///   <see cref="ArtifactTimeoutException" />, <see cref="ArtifactUpstreamException" />,
///   <see cref="ArtifactRateLimitedException" /> or <see cref="ArtifactStorageException" />.
/// </summary>
public abstract class ArtifactTransientException : Exception {
  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactTransientException" /> class.
  /// </summary>
  protected ArtifactTransientException() {
  }

  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactTransientException" /> class.
  /// </summary>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="inner_exception">The cause, if any.</param>
  protected ArtifactTransientException(string? message,
                                       Exception? inner_exception = null) :
    base(message, inner_exception) {
  }
}
