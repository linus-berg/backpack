using System.Collections.Generic;

namespace Integration.API.Output;

public class UserOutput {
  public string name { get; set; }
  public List<string> roles { get; set; } = new();
}
