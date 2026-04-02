namespace Library.Skopeo.Models;

public class SkopeoArchive {
  public SkopeoArchive(string remote_image, string target_dir) {
    Uri = new Uri(remote_image);
    Target = $"{Uri.Host}{Uri.PathAndQuery}";
    TarName = Target.Replace("/", "_").Replace(":", "_TAG_").Replace(".", "_");
    TarPath = Path.Join(target_dir, $"{TarName}.tar");
  }

  public Uri Uri { get; }

  public string Target { get; }

  public string TarPath { get; }

  public string TarName { get; }

  public string TarWithHost => $"{Path.Join(Uri.Host, TarName)}.tar";
}