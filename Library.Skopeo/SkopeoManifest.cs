using System.Text.Json.Serialization;

namespace Library.Skopeo;

public class SkopeoManifest {
  [JsonPropertyName("Name")]
  public required string name { get; set; }
}