using Core.Kernel.Messages;
using MassTransit;

namespace Tracker.Scheduler;

public class ReloadSchedulesConsumer : IConsumer<ReloadSchedulesRequest> {
  private readonly ScheduleManager schedule_manager_;

  public ReloadSchedulesConsumer(ScheduleManager schedule_manager) {
    schedule_manager_ = schedule_manager;
  }

  public async Task Consume(ConsumeContext<ReloadSchedulesRequest> context) {
    await schedule_manager_.ReloadSchedules();
  }
}
