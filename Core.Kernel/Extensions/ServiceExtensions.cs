// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Registrations;

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Kernel.Extensions;

/// <summary>
///   Provides extension methods for <see cref="IServiceCollection" /> to configure system telemetry.
/// </summary>
public static class ServiceExtensions {
  /// <summary>
  ///   Adds OpenTelemetry tracing and metrics to the service collection.
  /// </summary>
  /// <param name="s">The service collection to configure.</param>
  /// <param name="registration">The module registration information.</param>
  /// <returns>The configured service collection.</returns>
  public static IServiceCollection AddTelemetry(this IServiceCollection s,
                                                ModuleRegistration
                                                  registration) {
    if (!Configuration.HasOtelHost()) {
      return s;
    }

    void ConfigureRsc(ResourceBuilder r) {
      r.AddService(
        registration.name,
        serviceVersion: "master"
      );
      r.AddTelemetrySdk();
      r.AddEnvironmentVariableDetector();
    }

    s.AddOpenTelemetry()
     .ConfigureResource(ConfigureRsc)
     .WithTracing(
       builder => {
         builder.AddSource("Wolverine");
         builder.AddHttpClientInstrumentation();
         builder.AddRedisInstrumentation();
         builder.AddOtlpExporter(
           cfg => {
             cfg.Endpoint =
               new Uri(
                 Configuration.GetBackpackVariable(CoreVariables.BP_OTEL_HOST)
               );
             cfg.Protocol = OtlpExportProtocol.Grpc;
           }
         );
       }
     )
     .WithMetrics(
       builder => {
         builder.AddHttpClientInstrumentation();
         builder.AddRuntimeInstrumentation();
         builder.AddMeter("Wolverine");
         builder.AddOtlpExporter(
           cfg => {
             cfg.Endpoint =
               new Uri(
                 Configuration.GetBackpackVariable(CoreVariables.BP_OTEL_HOST)
               );
             cfg.Protocol = OtlpExportProtocol.Grpc;
           }
         );
       }
     );
    return s;
  }
}