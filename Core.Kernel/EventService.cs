using Core.Kernel.Messages;
using Core.Kernel.Models;
using MassTransit;

namespace Core.Kernel;

public class EventService : IEventService {
  private readonly ISendEndpointProvider bus_;

  public EventService(ISendEndpointProvider bus) {
    bus_ = bus;
  }

  public async Task LogEvent(string source, string message,
                             EventSeverity severity = EventSeverity.INFO,
                             string user = "System") {
    ISendEndpoint endpoint =
      await bus_.GetSendEndpoint(Endpoints.S_SYSTEM_EVENT);
    await endpoint.Send(
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