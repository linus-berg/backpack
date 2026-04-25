using Backpack.GitUnpack.Services;

namespace Backpack.GitUnpack;

public static class Program {
  public static void Main(string[] args) {
    IHost host = Host.CreateDefaultBuilder(args)
                     .ConfigureServices(
                       services => {
                         services.AddSingleton<Unpacker>();
                         services.AddHostedService<Worker>();
                       }
                     )
                     .Build();

    host.Run();
  }
}