// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Collector.Git;

/// <summary>
///   Represents a git repository and its local configuration.
/// </summary>
public class Repository {
  private readonly string original_uri_;
  private readonly UriBuilder uri_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Repository" /> class.
  /// </summary>
  /// <param name="repo">The repository URL.</param>
  /// <param name="local_directory">The base local directory for repositories.</param>
  public Repository(string repo, string local_directory) {
    original_uri_ = repo;
    uri_ = new UriBuilder(original_uri_) {
      Scheme = Uri.UriSchemeHttps,
      Port = -1
    };
    owner = GetOwner();
    remote = uri_.Uri.ToString();

    name = GetName();
    directory = GetDirectory();
    local_path = Path.Join(local_directory, directory);
  }

  /// <summary>
  ///   Gets the name of the repository.
  /// </summary>
  public string name { get; }

  /// <summary>
  ///   Gets the owner or organization of the repository.
  /// </summary>
  public string owner { get; }

  /// <summary>
  ///   Gets the remote URL of the repository.
  /// </summary>
  public string remote { get; }

  /// <summary>
  ///   Gets the relative directory path for the repository.
  /// </summary>
  public string directory { get; }

  /// <summary>
  ///   Gets the full local path to the repository mirror.
  /// </summary>
  public string local_path { get; }

  /// <summary>
  ///   Gets the name of the repository from the original URI.
  /// </summary>
  /// <returns>The repository name.</returns>
  private string GetName() {
    string filename = Path.GetFileName(original_uri_);
    if (original_uri_.EndsWith(".git")) {
      return filename.Substring(0, filename.Length - 4);
    }

    return filename;
  }

  /// <summary>
  ///   Gets the relative directory path.
  /// </summary>
  /// <returns>The directory path.</returns>
  private string GetDirectory() {
    return Path.Join(owner, name);
  }

  /// <summary>
  ///   Gets the owner/host part of the repository URL.
  /// </summary>
  /// <returns>The owner string.</returns>
  private string GetOwner() {
    string host = uri_.Host;
    string path = Path.GetDirectoryName(uri_.Uri.LocalPath);
    return Path.Join(host, path);
  }
}
