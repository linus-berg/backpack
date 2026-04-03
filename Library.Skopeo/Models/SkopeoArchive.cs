namespace Library.Skopeo.Models;

public class SkopeoArchive {
  public SkopeoArchive(string remote_image, string tag, string target_dir) {
    Uri uri = new Uri($"docker-archive://{remote_image}");
    Host = uri.Host;
    Tag = tag;
    Target = remote_image;
    TarName = $"{Target}:{Tag}".Replace("/", "_")
                               .Replace(":", "__COLON__")
                               .Replace(".", "__DOT__");
    TarPath = Path.Join(target_dir, $"{TarName}.tar");
  }

  public string Host { get; set; }
  public string Tag { get; }

  public string Target { get; }

  public string TarPath { get; }

  public string TarName { get; }

  public string TarWithHost => $"{Path.Join(Host, TarName)}.tar";
}