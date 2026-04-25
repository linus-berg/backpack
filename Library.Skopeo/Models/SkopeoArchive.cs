namespace Library.Skopeo.Models;

public class SkopeoArchive {
  public SkopeoArchive(string remote_image, string tag, string target_dir) {
    Uri uri = new Uri($"docker-archive://{remote_image}");
    host = uri.Host;
    this.tag = tag;
    target = remote_image;
    tar_name = $"{target}:{this.tag}".Replace("/", "_")
                                    .Replace(":", "__COLON__")
                                    .Replace(".", "__DOT__");
    tar_path = Path.Join(target_dir, $"{tar_name}.tar");
  }

  public string host { get; set; }
  public string tag { get; }

  public string target { get; }

  public string tar_path { get; }

  private string tar_name { get; }

  public string tar_with_host => $"{Path.Join(host, tar_name)}.tar";
}