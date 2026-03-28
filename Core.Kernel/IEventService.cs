using System.Threading.Tasks;
using Core.Kernel.Models;

namespace Core.Kernel;

public interface IEventService {
  public Task LogEvent(string source, string message,
                       EventSeverity severity = EventSeverity.INFO,
                       string user = "System");
}
