namespace Library.Skopeo;

public class SkopeoManifest {
  private string working_dir_;
  public string Name { get; set; }
  public string Digest { get; set; }
  public string Created { get; set; }
  public string Os { get; set; }
  public string Architecture { get; set; }
  public List<string> Layers { get; set; } = new();

  public string WorkingDirectory {
    get => working_dir_;
    set {
      working_dir_ = value;
      layer_dir_ = Path.Join(value, "shared", "sha256");
    }
  }

  private string layer_dir_ { get; set; }
}