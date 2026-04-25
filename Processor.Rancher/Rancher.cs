using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Library.Github;
using Library.Github.Models;
using RestSharp;

namespace Processor.Rancher;

public class Rancher : IRancher {
  private readonly IGithubClient gh_;
  private readonly RestClient http_client_ = new();
  private readonly ILogger<Rancher> logger_;

  public Rancher(IGithubClient gh, ILogger<Rancher> logger) {
    gh_ = gh;
    logger_ = logger;
  }

  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    List<GithubRelease> releases = await gh_.GetReleases(artifact.id);
    string file = artifact.config["file"];

    foreach (GithubRelease release in releases) {
      if (artifact.HasVersion(release.tag_name)) {
        continue;
      }

      string url = release.GetReleaseFile(file);
      if (string.IsNullOrEmpty(url)) {
        continue;
      }

      ArtifactVersion version = new() {
        version = release.tag_name
      };
      logger_.LogInformation("Release detected={Url}", url);
      string? rancher_file = await GetRancherFile(url);
      if (rancher_file == null) {
        throw new ArtifactMetadataException($"Could not get {url}");
      }

      string[] images = rancher_file.Split('\n');
      foreach (string image in images) {
        if (string.IsNullOrEmpty(image)) {
          continue;
        }

        string clean =
          image.Contains("docker.io")
            ? image
            : $"docker.io/{image}";
        version.AddFile(image, $"docker://{clean}");
      }

      artifact.AddVersion(version);
    }

    return artifact;
  }

  private async Task<string?> GetRancherFile(string url) {
    RestResponse response =
      await http_client_.GetAsync(new RestRequest(url));
    return response.Content ?? null;
  }
}