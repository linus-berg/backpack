namespace Processor.HuggingFace.Models;

public class HuggingFaceModel {
  public string id { get; set; } = string.Empty;
  public string sha { get; set; } = string.Empty;
  public List<HuggingFaceSibling>? siblings { get; set; }
}

public class HuggingFaceSibling {
  public string rfilename { get; set; } = string.Empty;
}