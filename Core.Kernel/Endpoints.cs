// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel;

/// <summary>
///   Defines system-wide message queue endpoints.
/// </summary>
public static class Endpoints {
  /// <summary>
  ///   Endpoint for processed gateway ingestion.
  /// </summary>
  public static readonly Uri S_GATEWAY_INGEST_PROCESSED =
    new("rabbitmq://queue/gateway-ingest-processed");

  /// <summary>
  ///   Endpoint for raw processed gateway ingestion.
  /// </summary>
  public static readonly Uri S_GATEWAY_INGEST_PROCESSED_RAW =
    new("rabbitmq://queue/gateway-ingest-processed-raw");

  /// <summary>
  ///   Endpoint for the collector router.
  /// </summary>
  public static readonly Uri S_COLLECTOR_ROUTER = new("rabbitmq://queue/collector-router");

  /// <summary>
  ///   Endpoint for unprocessed gateway ingestion.
  /// </summary>
  public static readonly Uri S_GATEWAY_INGEST_UNPROCESSED =
    new("rabbitmq://queue/gateway-ingest-unprocessed");

  /// <summary>
  ///   Endpoint for system events.
  /// </summary>
  public static readonly Uri S_SYSTEM_EVENT = new("rabbitmq://queue/system-event");
}