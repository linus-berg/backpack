// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Exceptions;

/// <summary>
/// Exception thrown when an operation on an artifact times out.
/// </summary>
public class ArtifactTimeoutException : Exception {
  /// <summary>
  /// Initializes a new instance of the <see cref="ArtifactTimeoutException"/> class.
  /// </summary>
  public ArtifactTimeoutException() {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="ArtifactTimeoutException"/> class with a specified error message.
  /// </summary>
  /// <param name="message">The message that describes the error.</param>
  public ArtifactTimeoutException(string? message) : base(message) {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="ArtifactTimeoutException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
  /// </summary>
  /// <param name="message">The error message that explains the reason for the exception.</param>
  /// <param name="inner_exception">The exception that is the cause of the current exception.</param>
  public ArtifactTimeoutException(string? message, Exception? inner_exception) :
    base(message, inner_exception) {
  }
}
