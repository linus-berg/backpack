namespace Core.Infrastructure.Models.RabbitMq;

public class RabbitMqQueue {
  public string name { get; set; }

  public string vhost { get; set; }

  public bool durable { get; set; }

  public bool auto_delete { get; set; }

  public bool exclusive { get; set; }

  public object arguments { get; set; }

  public string node { get; set; }

  public string state { get; set; }

  public int messages { get; set; }

  public int messages_ready { get; set; }

  public int messages_unacknowledged { get; set; }

  public int consumers { get; set; }

  public long memory { get; set; }

  public string type { get; set; }

  public RabbitMqQueueStats? message_stats { get; set; }
}