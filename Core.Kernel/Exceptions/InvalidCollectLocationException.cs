// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Exceptions;

/// <summary>
///   Thrown when a collection location is not an absolute URL with a scheme,
///   so no collector can be chosen for it (e.g. <c>github.com/owner/repo</c>
///   instead of <c>git://github.com/owner/repo</c>). The message is meant for
///   users and contains nothing but the location.
/// </summary>
public class InvalidCollectLocationException : ArgumentException {
  /// <summary>
  ///   Initializes a new instance of the <see cref="InvalidCollectLocationException" /> class.
  /// </summary>
  /// <param name="location">The rejected location.</param>
  public InvalidCollectLocationException(string location)
    : base(DescribeProblem(location)) {
    this.location = location;
  }

  /// <summary>
  ///   Gets the rejected location.
  /// </summary>
  public string location { get; }

  /// <summary>
  ///   Builds the user-facing explanation for a rejected location.
  /// </summary>
  /// <param name="location">The rejected location.</param>
  /// <returns>The message.</returns>
  public static string DescribeProblem(string location) {
    return
      $"'{location}' is not a valid location. Direct-collect artifacts are " +
      "identified by a URL that includes the protocol, e.g. " +
      "git://github.com/owner/repo.";
  }
}
