using System.Data;

namespace Core.Kernel;

public static class Configuration {
  private static readonly Dictionary<CoreVariables, string> S_DEFAULTS_ =
    new() {
      {
        CoreVariables.BP_API_HOST, "http://localhost:4001"
      }, {
        CoreVariables.BP_COLLECTOR_DIRECTORY, "/data/"
      }, {
        CoreVariables.BP_REDIS_HOST, "localhost"
      }, {
        CoreVariables.BP_REDIS_USER, "default"
      }, {
        CoreVariables.BP_REDIS_PASS, "myverylogpassword"
      }, {
        CoreVariables.BP_RABBIT_MQ_HOST, "localhost"
      }, {
        CoreVariables.BP_RABBIT_MQ_API, "http://localhost:15672"
      }, {
        CoreVariables.BP_RABBIT_MQ_USER, "guest"
      }, {
        CoreVariables.BP_RABBIT_MQ_PASS, "guest"
      }, {
        CoreVariables.BP_OTEL_HOST, "http://localhost:4318"
      }, {
        CoreVariables.BP_COLLECTOR_HTTP_DELTA, "true" // Create daily deltas
      }, {
        CoreVariables.BP_COLLECTOR_HTTP_MODE, "lake" // lake, forward 
      }
    };

  public static bool HasOtelHost() {
    bool has_otel_host = false;
    try {
      has_otel_host =
        !string.IsNullOrEmpty(GetBackpackVariable(CoreVariables.BP_OTEL_HOST));
    } catch {
      has_otel_host = false;
    }

    return has_otel_host;
  }

  public static string? GetBackpackVariable(CoreVariables variable) {
    string? value = Environment.GetEnvironmentVariable(variable.ToString());

    /* If variable is set */
    if (value != null) {
      return value;
    }

    /* If variable has default */
    if (S_DEFAULTS_.TryGetValue(variable, out string? default_value)) {
      return default_value;
    }

    throw new NoNullAllowedException($"{variable.ToString()} is null!");
  }
}