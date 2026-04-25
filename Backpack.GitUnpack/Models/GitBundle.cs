namespace Backpack.GitUnpack.Models;

public class GitBundle {
  public GitBundle(string filepath, string owner) {
    this.filepath = filepath;
    this.owner = owner;
    repository = Path.GetFileName(filepath);
    repository_dir =
      Path.Join(
        Environment.GetEnvironmentVariable("GIT_BUNDLE_REPOS"),
        owner,
        repository
      );
  }

  public string filepath { get; }
  public string repository { get; init; }
  public string repository_dir { get; init; }
  public string owner { get; }
}