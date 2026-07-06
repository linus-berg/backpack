using System.Net;
using System.Net.Http.Headers;
using Collector.Huggingface;
using Collector.Kernel;
using Core.Kernel;
using Core.Kernel.Constants;
using Core.Kernel.Extensions;
using Core.Kernel.Registrations;

ModuleRegistration registration = new(ModuleType.COLLECTOR, typeof(Consumer));
registration.AddEndpoint("hf");

IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices(
                   services => {
                     services.AddTelemetry(registration);
                     services.AddHttpClient("fetch-client")
                             .ConfigureHttpClient(
                               client => {
                                 client.DefaultRequestHeaders.UserAgent
                                       .ParseAdd("Backpack/1.0");
                                 
                                 /*
                                  * See the file utils/headers.py line 118 in the huggingface_hub python lib repo.
                                  */
                                 if (!string.IsNullOrEmpty(
                                       Environment.GetEnvironmentVariable(
                                         "BP_HF_TOKEN"
                                       )
                                     )) {
                                   client.DefaultRequestHeaders.Authorization =
                                     new AuthenticationHeaderValue(
                                       "Bearer",
                                       Environment.GetEnvironmentVariable(
                                         "BP_HF_TOKEN"
                                       )
                                     );
                                 }
                               }
                             );
                     services.AddStorage();
                     services.AddSingleton<FileSystem>();
                     services.Register(registration);
                     services.AddHostedService<Worker>();
                   }
                 )
                 .Build();

await host.RunAsync();