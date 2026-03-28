using Core.Kernel.Models;

namespace Core.Services;

public interface IStatusService {
  Task<List<QueueStatus>> QueueStatus();
  Task<bool> PurgeQueue(string queue_name);
}