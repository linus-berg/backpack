namespace Core.Infrastructure.Models.RabbitMq;

public class BackingQueueStatus {
  public double avg_ack_egress_rate { get; set; }

  public double avg_ack_ingress_rate { get; set; }

  public double avg_egress_rate { get; set; }

  public double avg_ingress_rate { get; set; }
}