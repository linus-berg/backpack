// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Exceptions;

/// <summary>
///   Exception thrown when there is an error processing artifact metadata.
/// </summary>
public class ArtifactMetadataException : Exception {
  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactMetadataException" /> class.
  /// </summary>
  public ArtifactMetadataException() {
  }

  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactMetadataException" /> class with a specified error message.
  /// </summary>
  /// <param name="message">The message that describes the error.</param>
  public ArtifactMetadataException(string? message) : base(message) {
  }

  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactMetadataException" /> class with a specified error message and a
  ///   reference to the inner exception that is the cause of this exception.
  /// </summary>
  /// <param name="message">The error message that explains the reason for the exception.</param>
  /// <param name="inner_exception">The exception that is the cause of the current exception.</param>
  public ArtifactMetadataException(string? message, Exception? inner_exception)
    : base(message, inner_exception) {
  }
}