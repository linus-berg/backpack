// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Registrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Core.Kernel.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IHostBuilder"/> to configure system logging.
/// </summary>
public static class HostBuilderExtensions {
  /// <summary>
  /// Adds OpenTelemetry logging to the host builder.
  /// </summary>
  /// <param name="builder">The host builder to configure.</param>
  /// <param name="registration">The module registration information.</param>
  /// <returns>The configured host builder.</returns>
  public static IHostBuilder AddLogging(this IHostBuilder builder,
                                        ModuleRegistration registration) {
    if (!Configuration.HasOtelHost()) {
      return builder;
    }

    builder.ConfigureLogging(
      logs => {
        logs.ClearProviders();
        logs.AddOpenTelemetry(
          otel => {
            otel.IncludeScopes = true;
            ResourceBuilder resource_builder =
              ResourceBuilder
                .CreateDefault()
                .AddService(registration.name);
            otel.SetResourceBuilder(resource_builder)
                .AddOtlpExporter(
                  exporter => {
                    exporter.Endpoint =
                      new Uri(
#pragma warning disable CS8604 // Possible null reference argument.
                        Configuration.GetBackpackVariable(CoreVariables.BP_OTEL_HOST)
                      );
#pragma warning restore CS8604 // Possible null reference argument.
                    exporter.Protocol =
                      OtlpExportProtocol.Grpc;
                  }
                )
                .AddConsoleExporter();
          }
        );
      }
    );
    return builder;
  }
}
