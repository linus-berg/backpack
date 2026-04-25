using System.Text.RegularExpressions;

namespace Library.Github.Models;

public class GithubRelease {
  public required string tag_name { get; set; }
  public bool draft { get; set; }
  public bool prerelease { get; set; }
  public required List<GithubReleaseAsset> assets { get; set; }

  public string GetReleaseFile(string name) {
    foreach (GithubReleaseAsset asset in assets) {
      if (asset.name == name) {
        return asset.browser_download_url;
      }
    }

    return string.Empty;
  }
}