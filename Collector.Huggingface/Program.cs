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
                     services.AddDownloadClient(
                       "fetch-client",
                       client => {
                         /*
                          * See the file utils/headers.py line 118 in the huggingface_hub python lib repo.
                          */
                         string? hf_token =
                           Environment.GetEnvironmentVariable("BP_HF_TOKEN");
                         if (!string.IsNullOrEmpty(hf_token)) {
                           client.DefaultRequestHeaders.Authorization =
                             new AuthenticationHeaderValue("Bearer", hf_token);
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