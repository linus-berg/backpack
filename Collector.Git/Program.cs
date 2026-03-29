// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Git;
using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Polly;
using Polly.Timeout;

ModuleRegistration registration = new(ModuleType.COLLECTOR, typeof(Consumer));
registration.AddEndpoint("git");

IHost host = Host.CreateDefaultBuilder(args)
                 .AddLogging(registration)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddStorage();
                     services.AddResiliencePipeline<string, bool>(
                       "git-timeout",
                       builder => {
                         builder.AddTimeout(
                           new
                             TimeoutStrategyOptions {
                               Timeout =
                                 TimeSpan.FromMinutes(120)
                             }
                         );
                       }
                     );
                     services.AddSingleton<FileSystem>();
                     services.AddSingleton<Git>();
                     services.Register(registration);
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();