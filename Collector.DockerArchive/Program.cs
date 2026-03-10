// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.DockerArchive;
using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Library.Skopeo;
using Polly;
using Polly.Retry;

ModuleRegistration registration = new(ModuleType.COLLECTOR, typeof(Consumer));
registration.AddEndpoint("docker-archive", 1);
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTelemetry(registration);
builder.Services.AddStorage();
builder.Services.AddResiliencePipeline<string, bool>(
  "skopeo-retry",
  pipeline_builder => {
    pipeline_builder.AddRetry(
      new RetryStrategyOptions<bool> {
        MaxRetryAttempts = 20
      }
    );
  }
);
builder.Services.AddSingleton<SkopeoClient>();
builder.Services.AddSingleton<FileSystem>();
builder.Services.AddSingleton<Docker>();
builder.Services.Register(registration);
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
