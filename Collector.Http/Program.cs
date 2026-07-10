// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Http;
using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
ModuleRegistration registration = new(ModuleType.COLLECTOR, typeof(Consumer));
registration.AddEndpoint("http");
registration.AddEndpoint("https");

IHost host = Host.CreateDefaultBuilder(args)
                 .UseBackpackWolverine(registration)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddHttpClient("fetch-client")
                             .ConfigureHttpClient(
                               client => {
                                 client.DefaultRequestHeaders.UserAgent
                                       .ParseAdd("Backpack/1.0");
                               }
                             );
                     services.AddStorage();
                     services.AddSingleton<FileSystem>();
                     
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();