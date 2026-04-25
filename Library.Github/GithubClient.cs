using Library.Github.Models;
using RestSharp;
using RestSharp.Authenticators;

namespace Library.Github;

public class GithubClient : IGithubClient {
  private const string C_API_ = "https://api.github.com";

  private const int C_PAGE_SIZE_ = 10;
  private const int C_MAX_RELEASES_ = 100;
  private readonly RestClient client_ = new(C_API_);

  public GithubClient() {
    string? api_key =
      Environment.GetEnvironmentVariable("GITHUB_API_KEY");
    if (!string.IsNullOrEmpty(api_key)) {
      RestClientOptions opts = new(C_API_) {
        Authenticator = new JwtAuthenticator(api_key)
      };
      client_ = new RestClient(opts);
    }
  }

  public async Task<List<GithubRelease>> GetReleases(string repo) {
    List<GithubRelease> releases = new();
    for (int page = 1; releases.Count < C_MAX_RELEASES_; page++) {
      List<GithubRelease>? page_releases = await GetReleasePage(repo, page);
      if (page_releases == null || page_releases.Count == 0) {
        break;
      }

      releases.AddRange(
        page_releases.Where(release => !release.prerelease && !release.draft)
      );
    }

    return releases;
  }

  private async Task<List<GithubRelease>?>
    GetReleasePage(string repo, int page, int page_size = 10) {
    try {
      return await client_.GetAsync<List<GithubRelease>>(
               $"/repos/{repo}/releases?page={page}&per_page={page_size}",
               new {
                 page
               }
             );
    } catch (Exception e) {
      Console.WriteLine(e);
      throw;
    }
  }
}