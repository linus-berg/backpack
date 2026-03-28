using Core.Kernel.Messages;
using Core.Kernel.Models;
using Core.Services;
using MassTransit;
using Event = Core.Kernel.Models.Event;

namespace Core.Gateway;

public class SystemEventConsumer : IConsumer<SystemEventMessage> {
  private readonly ICoreDatabase database_;
  private readonly ILogger<SystemEventConsumer> logger_;

  public SystemEventConsumer(ILogger<SystemEventConsumer> logger, ICoreDatabase database) {
    logger_ = logger;
    database_ = database;
  }

  public async Task Consume(ConsumeContext<SystemEventMessage> context) {
    SystemEventMessage message = context.Message;
    
    await database_.AddEvent(new Event {
      source = message.source,
      message = message.message,
      severity = message.severity,
      user = message.user,
      timestamp = message.timestamp
    });
    
    logger_.LogDebug("Event logged from {Source}: {Message}", message.source, message.message);
  }
}
