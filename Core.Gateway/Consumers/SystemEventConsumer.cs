using Core.Kernel.Messages;
using Core.Services;
using Wolverine;
using Event = Core.Kernel.Models.Event;

namespace Core.Gateway.Consumers;

public class SystemEventConsumer {
  private readonly ICoreDatabase database_;
  private readonly ILogger<SystemEventConsumer> logger_;

  public SystemEventConsumer(ILogger<SystemEventConsumer> logger,
                             ICoreDatabase database) {
    logger_ = logger;
    database_ = database;
  }

  public async Task Handle(SystemEventMessage request, IMessageContext context) {
    SystemEventMessage message = request;

    await database_.AddEvent(
      new Event {
        source = message.source,
        message = message.message,
        severity = message.severity,
        user = message.user,
        timestamp = message.timestamp
      }
    );

    logger_.LogDebug(
      "Event logged from {Source}: {Message}",
      message.source,
      message.message
    );
  }
}