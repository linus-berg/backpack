namespace Core.Infrastructure.Models.RabbitMq;

public class RabbitMqQueue {
  public required string name { get; set; }

  public int messages { get; set; }
  /* Waiting for a consumer. */
  public int messages_ready { get; set; }
  /* Delivered to a consumer and not yet acknowledged: being processed. */
  public int messages_unacknowledged { get; set; }

  public int consumers { get; set; }

  public RabbitMqQueueStats? message_stats { get; set; }
}