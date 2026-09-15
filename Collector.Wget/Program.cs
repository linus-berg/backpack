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
                     /* The client itself is left unbounded on purpose: WebMirror
                        applies its own per-request and per-stream limits, and a
                        ceiling here would silently cut a mirror short before
                        those ever came into play. */
                     services.AddDownloadClient(
                       "mirror-client",
                       configure_handler: handler => {
                         handler.AllowAutoRedirect = true;
                         handler.MaxAutomaticRedirections = 10;
                         /* Unlike the artifact collectors, the mirror parses what
                            it fetches to rewrite links, so it wants the decoded
                            body rather than the bytes as served. */
                         handler.AutomaticDecompression =
                           System.Net.DecompressionMethods.All;
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