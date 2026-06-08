namespace Collector.Huggingface;

public class Worker : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stopping_token) {
    while (!stopping_token.IsCancellationRequested) {
      await Task.Delay(1000, stopping_token);
    }
  }
}