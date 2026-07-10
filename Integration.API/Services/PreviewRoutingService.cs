using Core.Kernel.Messages;
using Wolverine;

namespace Integration.API.Services;

public class PreviewRoutingService {
  private readonly IMessageBus bus_;

  public PreviewRoutingService(IMessageBus bus) {
    bus_ = bus;
  }

  public async Task<ArtifactPreviewResponse> GetDataDynamicallyAsync(
    string target_queue_name, ArtifactPreviewRequest request_data) {
    // 1. Construct the destination URI. 
    Uri destination_address = new($"rabbitmq://queue/{target_queue_name}");

    // 2. Send the request and wait for the response
    return await bus_.EndpointFor(destination_address)
                     .InvokeAsync<ArtifactPreviewResponse>(request_data);
  }
}