// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Library.Skopeo;
using Processor.Container;

ModuleRegistration registration = new(ModuleType.PROCESSOR, typeof(Consumer));
registration.AddEndpoint("container");
registration.AddEndpoint("container-preview");

IHost host = Host.CreateDefaultBuilder(args)
                 .UseBackpackWolverine(registration)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddSingleton<SkopeoClient>();
                     
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();