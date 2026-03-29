// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Models;

/// <summary>
///   Represents the current status of a message queue.
/// </summary>
public class QueueStatus {
  /// <summary>
  ///   Gets or sets the name of the queue.
  /// </summary>
  public required string name { get; set; }

  /// <summary>
  ///   Gets or sets the number of messages in the queue.
  /// </summary>
  public required long messages { get; set; } = 0;

  /// <summary>
  ///   Gets or sets the number of active consumers for the queue.
  /// </summary>
  public required int consumers { get; set; } = 0;

  /// <summary>
  ///   Gets or sets the average egress rate of messages from the queue.
  /// </summary>
  public required double? avg_egress_rate { get; set; } = 0;

  /// <summary>
  ///   Gets or sets the average ingress rate of messages into the queue.
  /// </summary>
  public required double? avg_ingress_rate { get; set; } = 0;
}