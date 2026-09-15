// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel;

/// <summary>
///   Defines environment variables used for system configuration.
/// </summary>
public enum CoreVariables {
  /// <summary>
  ///   RabbitMQ Management API endpoint.
  /// </summary>
  BP_RABBIT_MQ_API,

  /// <summary>
  ///   RabbitMQ host.
  /// </summary>
  BP_RABBIT_MQ_HOST,

  /// <summary>
  ///   RabbitMQ username.
  /// </summary>
  BP_RABBIT_MQ_USER,

  /// <summary>
  ///   RabbitMQ password.
  /// </summary>
  BP_RABBIT_MQ_PASS,

  /// <summary>
  ///   Redis host.
  /// </summary>
  BP_REDIS_HOST,

  /// <summary>
  ///   Redis username.
  /// </summary>
  BP_REDIS_USER,

  /// <summary>
  ///   Redis password.
  /// </summary>
  BP_REDIS_PASS,

  /// <summary>
  ///   OpenTelemetry collector endpoint.
  /// </summary>
  BP_OTEL_HOST,

  /// <summary>
  ///   Backpack API host.
  /// </summary>
  BP_API_HOST,

  /// <summary>
  ///   Directory for collector storage (Legacy).
  /// </summary>
  BP_COLLECTOR_DIRECTORY,

  /// <summary>
  ///   Wall-clock budget for a single artifact download, as a
  ///   <see cref="TimeSpan" /> string (for example <c>02:00:00</c>). Covers the
  ///   request, the transfer of the body and the upload to storage. Keep it
  ///   below the MassTransit consumer timeout, otherwise the broker gives up on
  ///   the message before the download is abandoned.
  /// </summary>
  BP_COLLECTOR_DOWNLOAD_TIMEOUT,

  /// <summary>
  ///   Time allowed to establish a TCP connection to a remote host, as a
  ///   <see cref="TimeSpan" /> string (for example <c>00:00:30</c>). Bounds the
  ///   connect phase only, so an unreachable host fails fast instead of
  ///   consuming the whole download budget.
  /// </summary>
  BP_COLLECTOR_CONNECT_TIMEOUT,

  /// <summary>
  ///   Port the health endpoint listens on for liveness and readiness probes.
  /// </summary>
  BP_HEALTH_PORT,

  /// <summary>
  ///   MongoDB connection string.
  /// </summary>
  BP_MONGO_STR,

  /// <summary>
  ///   S3 access key.
  /// </summary>
  BP_S3_ACCESS_KEY,

  /// <summary>
  ///   S3 secret key.
  /// </summary>
  BP_S3_SECRET_KEY,

  /// <summary>
  ///   S3 region.
  /// </summary>
  BP_S3_REGION,

  /// <summary>
  ///   S3 endpoint.
  /// </summary>
  BP_S3_ENDPOINT,

  /// <summary>
  ///   S3 bucket name.
  /// </summary>
  BP_S3_BUCKET,
  
  /// <summary>
  ///   S3 debug tracing enabled.
  /// </summary>
  BP_S3_TRACING,

  /// <summary>
  ///   Flag for daily delta collection in HTTP collector.
  /// </summary>
  BP_COLLECTOR_HTTP_DELTA,

  /// <summary>
  ///   Operation mode for HTTP collector (e.g., lake, forward).
  /// </summary>
  BP_COLLECTOR_HTTP_MODE,

  /// <summary>
  ///   Container registry proxy for S3.
  /// </summary>
  BP_COLLECTOR_CONTAINER_REGISTRY,

  /// <summary>
  ///   OIDC authority
  /// </summary>
  BP_OIDC_AUTHORITY,

  /// <summary>
  ///   OIDC audience
  /// </summary>
  BP_OIDC_AUDIENCE
}