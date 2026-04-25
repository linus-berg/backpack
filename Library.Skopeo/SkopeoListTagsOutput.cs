using System.Text.Json.Serialization;

namespace Library.Skopeo;

public class SkopeoListTagsOutput {
  [JsonPropertyName("Repository")]
  public required string repository { get; set; }
  [JsonPropertyName("Tags")]
  public required IEnumerable<string> tags { get; set; }
}