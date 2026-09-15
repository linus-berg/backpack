using Backpack.GitUnpack.Services;
using Core.Kernel.Extensions;

namespace Backpack.GitUnpack;

public static class Program {
  public static void Main(string[] args) {
    IHost host = Host.CreateDefaultBuilder(args)
                     .ConfigureServices(
                       services => {
                         /* Liveness only: this service has no broker or storage
                            of its own, so there is nothing to be ready for. */
                         services.AddBackpackHealthEndpoint();
                         services.AddSingleton<Unpacker>();
                         services.AddHostedService<Worker>();
                       }
                     )
                     .Build();

    host.Run();
  }
}