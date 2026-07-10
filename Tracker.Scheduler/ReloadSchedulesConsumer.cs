using Core.Kernel.Messages;

namespace Tracker.Scheduler;

public class ReloadSchedulesConsumer {
  private readonly ScheduleManager schedule_manager_;

  public ReloadSchedulesConsumer(ScheduleManager schedule_manager) {
    schedule_manager_ = schedule_manager;
  }

  public async Task Handle(ReloadSchedulesRequest request) {
    await schedule_manager_.ReloadSchedules();
  }
}