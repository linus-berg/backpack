// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Registrations;

/// <summary>
///   Represents a message queue endpoint with a specific name and concurrency level.
/// </summary>
public class Endpoint {
  /// <summary>
  ///   Gets or sets the name of the endpoint.
  /// </summary>
  public required string name { get; set; }

  /// <summary>
  ///   Gets or sets the maximum number of concurrent messages to process for this endpoint.
  /// </summary>
  public int concurrency { get; set; } = 10;
}