// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;
using Processor.HuggingFace;

ModuleRegistration registration = new(ModuleType.PROCESSOR, typeof(Consumer));
registration.AddEndpoint("huggingface");
registration.AddEndpoint("huggingface-preview");

IHost host = Host.CreateDefaultBuilder(args)
                 .UseBackpackWolverine(registration)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddSingleton<IHuggingFace, HuggingFace>();
                     
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();