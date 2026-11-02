// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Exceptions;

/// <summary>
///   Backpack's own storage failed: S3 is unreachable or erroring, or the
///   local temp disk is full. Unlike an upstream failure this affects every
///   message. Collector.Kernel's FileSystem waits up to 10 minutes for S3
///   before throwing it, which pauses the collector; the message is then not
///   retried at once but redelivered every 15 minutes for up to a day.
/// </summary>
public class ArtifactStorageException : ArtifactTransientException {
  /// <summary>
  ///   Initializes a new instance of the <see cref="ArtifactStorageException" /> class.
  /// </summary>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="inner_exception">The cause, if any.</param>
  public ArtifactStorageException(string? message,
                                  Exception? inner_exception = null) :
    base(message, inner_exception) {
  }
}
