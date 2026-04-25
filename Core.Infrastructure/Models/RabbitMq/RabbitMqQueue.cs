namespace Core.Infrastructure.Models.RabbitMq;

public class RabbitMqQueue {
  public required string name { get; set; }

  public int messages { get; set; }

  public int consumers { get; set; }

  public RabbitMqQueueStats? message_stats { get; set; }
}