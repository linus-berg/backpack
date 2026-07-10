using Core.Kernel.Messages;
using Core.Kernel.Models;
using Wolverine;

namespace Core.Kernel;

public class EventService : IEventService {
  private readonly IMessageBus bus_;

  public EventService(IMessageBus bus) {
    bus_ = bus;
  }

  public async Task LogEvent(string source, string message,
                             EventSeverity severity = EventSeverity.INFO,
                             string user = "System") {
    await bus_.EndpointFor(Endpoints.S_SYSTEM_EVENT).SendAsync(
      new SystemEventMessage {
        source = source,
        message = message,
        severity = severity,
        user = user,
        timestamp = DateTime.UtcNow
      }
    );
  }
}