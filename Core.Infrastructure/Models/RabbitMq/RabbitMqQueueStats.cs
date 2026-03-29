namespace Core.Infrastructure.Models.RabbitMq;

public class RabbitMqQueueStats {
  public RabbitMqStatsDetails? ack_details { get; set; }
  public RabbitMqStatsDetails? publish_details { get; set; }
}