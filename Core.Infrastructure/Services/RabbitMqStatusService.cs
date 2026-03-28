using Core.Infrastructure.Models.RabbitMq;
using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using RestSharp;
using RestSharp.Authenticators;

namespace Core.Infrastructure.Services;

public class RabbitMqStatusService : IStatusService {
  private RestClient client_;

  public RabbitMqStatusService() {
    RestClientOptions options = new() {
      BaseUrl = new Uri(
        Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_API) ??
        throw new InvalidOperationException()
      ),
      Authenticator = new HttpBasicAuthenticator(
        Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_USER) ??
        throw new InvalidOperationException(),
        Configuration.GetBackpackVariable(CoreVariables.BP_RABBIT_MQ_PASS) ??
        throw new InvalidOperationException()
      )
    };
    client_ = new RestClient(options);
    //client_.AddDefaultHeader("Accept", "application/json");
  }

  public async Task<List<QueueStatus>> QueueStatus() {
    List<RabbitMqQueue>? queues =
      await client_.GetAsync<List<RabbitMqQueue>>("api/queues");
    if (queues == null) {
      return new();
    }

    List<QueueStatus> queue_statuses = new();
    foreach (RabbitMqQueue queue in queues) {
      if (queue.name.Contains("error")) {
        continue;
      }

      queue_statuses.Add(
        new QueueStatus() {
          name = queue.name,
          consumers = queue.consumers,
          messages = queue.messages,
          avg_egress_rate = queue.message_stats?.ack_details?.rate ?? 0,
          avg_ingress_rate = queue.message_stats?.publish_details?.rate ?? 0
        }
      );
    }

    return queue_statuses;
  }

  public async Task<bool> PurgeQueue(string queue_name) {
    // 1. URL-encode the vhost and queue name. 
    // This is strictly required because the default vhost is "/" (which encodes to "%2f").
    string encoded_vhost = Uri.EscapeDataString("/");
    string encoded_queue = Uri.EscapeDataString(queue_name);

    // 2. Configure the RestClient options with Basic Authentication

    // 3. Set up the DELETE request to the /contents endpoint
    string endpoint = $"api/queues/{encoded_vhost}/{encoded_queue}/contents";
    RestRequest request = new RestRequest(endpoint, Method.Delete);
    request.AddHeader("Accept", "*/*");

    // 4. Execute the request
    RestResponse response = await client_.ExecuteAsync(request);

    // 5. Handle the result
    if (response.IsSuccessful) {
      Console.WriteLine(
        $"Successfully purged queue: '{queue_name}' on vhost: '{encoded_vhost}'."
      );
    } else {
      Console.WriteLine(
        $"Failed to purge queue. Status Code: {response.StatusCode}"
      );
      Console.WriteLine($"Error Message: {response.ErrorMessage}");
      Console.WriteLine($"Content: {response.Content}");
    }

    return response.IsSuccessStatusCode;
  }
}