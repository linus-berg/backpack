// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Collector.Kernel;
using Collector.Wget;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Registrations;
using Foundatio.Storage;

ModuleRegistration registration = new(ModuleType.COLLECTOR, typeof(Consumer));
registration.AddEndpoint("wget");
IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices(
                   services => {
                     services.AddSingleton<IFileStorage>(
                       new FolderFileStorage(
                         b => {
                           b.Folder(
                             Configuration.GetBackpackVariable(
                               CoreVariables.BP_COLLECTOR_DIRECTORY
                             )
                           );
                           return b;
                         }
                       )
                     );
                     services.AddSingleton<FileSystem>();
                     services.AddSingleton<Wget>();
                     services.Register(registration);
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();