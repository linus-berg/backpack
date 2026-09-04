// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Data;

namespace Core.Kernel;

/// <summary>
///   Provides central configuration management using environment variables and default values.
/// </summary>
public static class Configuration {
  private static readonly Dictionary<CoreVariables, string> S_DEFAULTS_ =
    new() {
      {
        CoreVariables.BP_API_HOST, "http://localhost:4001"
      }, {
        CoreVariables.BP_COLLECTOR_DIRECTORY, "/data/"
      }, {
        CoreVariables.BP_REDIS_HOST, "localhost"
      }, {
        CoreVariables.BP_REDIS_USER, "default"
      }, {
        CoreVariables.BP_REDIS_PASS, "myverylongpassword"
      }, {
        CoreVariables.BP_RABBIT_MQ_HOST, "localhost"
      }, {
        CoreVariables.BP_RABBIT_MQ_API, "http://localhost:15672"
      }, {
        CoreVariables.BP_RABBIT_MQ_USER, "guest"
      }, {
        CoreVariables.BP_RABBIT_MQ_PASS, "guest"
      }, {
        CoreVariables.BP_OTEL_HOST, "http://localhost:4318"
      }, {
        CoreVariables.BP_COLLECTOR_HTTP_DELTA, "true" // Create daily deltas
      }, {
        CoreVariables.BP_COLLECTOR_HTTP_MODE, "lake" // lake, forward 
      }, {
        CoreVariables.BP_OIDC_AUTHORITY, "http://localhost:8090/realms/backpack"
      }, {
        CoreVariables.BP_OIDC_AUDIENCE, "backpack"
      }, {
        CoreVariables.BP_S3_TRACING, "false"
      }
    };

  /// <summary>
  ///   Checks if the OpenTelemetry host configuration is available.
  /// </summary>
  /// <returns>True if the host is configured; otherwise, false.</returns>
  public static bool HasOtelHost() {
    bool has_otel_host = false;
    try {
      has_otel_host =
        !string.IsNullOrEmpty(GetBackpackVariable(CoreVariables.BP_OTEL_HOST));
    } catch {
      has_otel_host = false;
    }

    return has_otel_host;
  }

  /// <summary>
  ///   Retrieves a configuration variable from environment variables or its default value.
  /// </summary>
  /// <param name="variable">The variable to retrieve.</param>
  /// <returns>The value of the configuration variable.</returns>
  /// <exception cref="NoNullAllowedException">Thrown when the variable is not set and has no default value.</exception>
  public static string GetBackpackVariable(CoreVariables variable) {
    string? value = Environment.GetEnvironmentVariable(variable.ToString());

    /* If variable is set */
    if (value != null) {
      return value;
    }

    /* If variable has default */
    if (S_DEFAULTS_.TryGetValue(variable, out string? default_value)) {
      return default_value;
    }

    throw new NoNullAllowedException($"{variable.ToString()} is null!");
  }
}