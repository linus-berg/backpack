// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Collector.Wget;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;

ModuleRegistration registration = new(ModuleType.COLLECTOR, typeof(Consumer));
registration.AddEndpoint("wget");
IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddHttpClient("mirror-client")
                             .ConfigureHttpClient(
                               client => {
                                 client.DefaultRequestHeaders.UserAgent
                                       .ParseAdd("Backpack/1.0");
                                 client.Timeout = TimeSpan.FromMinutes(5);
                               }
                             )
                             .ConfigurePrimaryHttpMessageHandler(
                               () => new HttpClientHandler {
                                 AllowAutoRedirect = true,
                                 MaxAutomaticRedirections = 10,
                                 AutomaticDecompression =
                                   System.Net.DecompressionMethods.All
                               }
                             );
                     services.AddStorage();
                     services.AddSingleton<FileSystem>();
                     services.AddSingleton<WebMirror>();
                     services.Register(registration);
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();