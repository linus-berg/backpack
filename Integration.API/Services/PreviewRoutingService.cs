using Core.Kernel.Messages;
using MassTransit;

namespace Integration.API.Services;

public class PreviewRoutingService {
  private readonly IClientFactory client_factory_;

  public PreviewRoutingService(IClientFactory client_factory) {
    client_factory_ = client_factory;
  }

  public async Task<ArtifactPreviewResponse> GetDataDynamicallyAsync(
    string target_queue_name, ArtifactPreviewRequest request_data) {
    // 1. Construct the destination URI. 
    // Using the "queue:" scheme is the modern, transport-independent way in MassTransit 8+.
    Uri destination_address = new($"queue:{target_queue_name}");

    // 2. Create the RequestClient targeting that specific address
    IRequestClient<ArtifactPreviewRequest> client =
      client_factory_.CreateRequestClient<ArtifactPreviewRequest>(
        destination_address
      );

    // 3. Send the request and wait for the response
    Response<ArtifactPreviewResponse> response =
      await client.GetResponse<ArtifactPreviewResponse>(request_data);

    return response.Message;
  }
}