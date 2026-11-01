// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Core.Kernel.Health;

/// <summary>
///   Tags of health checks.
/// </summary>
public static class HealthTags {
  /// <summary>
  ///   Checks that /healthz/ready runs: the dependencies a service needs to
  ///   do its work. MassTransit tags its bus check with the same name.
  /// </summary>
  public const string READY = "ready";

  /// <summary>
  ///   The tag MassTransit puts on its bus check.
  /// </summary>
  public const string MASSTRANSIT = "masstransit";

  /// <summary>
  ///   Checks of the infrastructure Backpack depends on (RabbitMQ, MongoDB,
  ///   Redis, S3, the OIDC provider), which Integration.API reports to the
  ///   GUI's Status page.
  /// </summary>
  public const string INFRASTRUCTURE = "infrastructure";

  /// <summary>
  ///   <see cref="READY" /> and <see cref="INFRASTRUCTURE" />.
  /// </summary>
  public static readonly string[] READY_INFRASTRUCTURE = { READY, INFRASTRUCTURE };
}
