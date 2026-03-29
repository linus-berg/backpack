// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using MavenNet;
using MavenNet.Models;
using Processor.Maven.Models;
using RestSharp;
using Artifact = Core.Kernel.Models.Artifact;

namespace Processor.Maven;

/// <summary>
///   Implementation of Maven artifact processing.
/// </summary>
public class Maven : IMaven {
  private const string C_MAVEN_ = "https://repo1.maven.org/maven2";
  private const string C_MAVEN_SEARCH_ = "https://search.maven.org";
  private readonly ILogger<Maven> logger_;
  private readonly RestClient mvn_search_ = new(C_MAVEN_SEARCH_);
  private readonly MavenCentralRepository repo_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Maven" /> class.
  /// </summary>
  /// <param name="logger">The logger instance.</param>
  public Maven(ILogger<Maven> logger) {
    logger_ = logger;
    repo_ = MavenRepository.FromMavenCentral();
  }

  /// <summary>
  ///   Processes the artifact to find Maven versions and dependencies.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    string group_id = GetGroup(artifact);
    string artifact_id = artifact.id;
    Metadata metadata = await GetMetadata(group_id, artifact_id);
    Dictionary<string, List<string>> search =
      await SearchMaven(group_id, artifact_id);
    if (metadata == null) {
      throw new ArtifactMetadataException();
    }

    foreach (string version in metadata.AllVersions) {
      if (artifact.HasVersion(version)) {
        continue;
      }

      /* Get pom for version */
      Project project = await GetPom(group_id, artifact_id, version);
      if (project == null) {
        continue;
      }

      Dictionary<string, string> properties = GetProperties(project);
      /* Add pom and lib to collection */
      ArtifactVersion artifact_version = new() {
        version = version
      };
      if (!search.ContainsKey(version)) {
        continue;
      }

      List<string> files = search[version];
      foreach (string file in files) {
        artifact_version.AddFile(
          file,
          GetFile(group_id, artifact.id, version, file)
        );
      }

      await AddDependencies(artifact, project, properties);
      await AddPlugins(artifact, project, properties);
      artifact.AddVersion(artifact_version);
    }

    return artifact;
  }

  /// <summary>
  ///   Searches Maven for versions and files of an artifact.
  /// </summary>
  /// <param name="g">The group identifier.</param>
  /// <param name="id">The artifact identifier.</param>
  /// <returns>A task that represents the search operation, containing a dictionary of versions and their file extensions.</returns>
  public async Task<Dictionary<string, List<string>>> SearchMaven(
    string g, string id) {
    string group = g.Replace("/", ".");
    Dictionary<string, List<string>> versions = new();
    int count = 0;
    int start = 0;
    do {
      MavenSearch search = await mvn_search_.GetJsonAsync<MavenSearch>(
                             $"/solrsearch/select?q=g:{group}+AND+a:{id}&core=gav&rows=200&wt=json&start={start}"
                           );
      count = search.response.docs.Count;
      start += 200;

      foreach (MavenSearchDoc doc in search.response.docs) {
        versions[doc.v] = doc.ec;
      }
    } while (count > 0);

    return versions;
  }

  /// <summary>
  ///   Gets metadata for a Maven artifact.
  /// </summary>
  /// <param name="g">The group identifier.</param>
  /// <param name="id">The artifact identifier.</param>
  /// <returns>A task that represents the metadata retrieval operation.</returns>
  public async Task<Metadata> GetMetadata(string g, string id) {
    Metadata m = null;
    try {
      await using Stream s = await repo_.OpenMavenMetadataFile(g, id);
      m = Parse<Metadata>(s);
    } catch (Exception e) {
      logger_.LogError(e.ToString());
    }

    return m;
  }

  /// <summary>
  ///   Gets the POM (Project Object Model) for a specific version of a Maven artifact.
  /// </summary>
  /// <param name="g">The group identifier.</param>
  /// <param name="id">The artifact identifier.</param>
  /// <param name="v">The version string.</param>
  /// <returns>A task that represents the POM retrieval operation.</returns>
  public async Task<Project> GetPom(string g, string id, string v) {
    Project p = null;
    try {
      await using Stream s = await repo_.OpenArtifactPomFile(g, id, v);
      p = Parse<Project>(s, false);
    } catch (Exception e) {
      logger_.LogError(e.ToString());
    }

    return p;
  }

  /// <summary>
  ///   Constructs the URL for a Maven artifact file.
  /// </summary>
  /// <param name="group">The group identifier.</param>
  /// <param name="id">The artifact identifier.</param>
  /// <param name="version">The version string.</param>
  /// <param name="extension">The file extension.</param>
  /// <returns>the URL to the file.</returns>
  public string GetFile(string group, string id, string version,
                        string extension) {
    return Path.Combine(
      C_MAVEN_,
      group,
      id,
      version,
      $"{id}-{version}{extension}"
    );
  }

  private async Task AddDependencies(Artifact artifact, Project project,
                                     Dictionary<string, string> properties) {
    foreach (Dependency dep in project.Dependencies) {
      string id = ReplaceProperties(dep.ArtifactId, properties);
      ArtifactDependency dependency =
        artifact.AddDependency(id, "maven");
      if (dep.GroupId.Contains("${project.groupId}")) {
        dependency.config["group"] = project.GroupId ?? project.Parent.GroupId;
      } else {
        string gid = ReplaceProperties(dep.GroupId, properties);
        dependency.config["group"] = gid;
      }
    }
  }


  private async Task AddPlugins(Artifact artifact, Project project,
                                Dictionary<string, string> properties) {
    if (project.Build is not { PluginsSpecified: true }) {
      return;
    }

    foreach (Plugin plugin in project.Build.Plugins) {
      string id = ReplaceProperties(plugin.ArtifactId, properties);
      ArtifactDependency dependency = artifact.AddDependency(id, "maven");
      if (plugin.GroupId.Contains("${project.groupId}")) {
        dependency.config["group"] = project.GroupId ?? project.Parent.GroupId;
      } else {
        string gid = ReplaceProperties(plugin.GroupId, properties);
        dependency.config["group"] = gid;
      }
    }
  }

  private Dictionary<string, string> GetProperties(Project project) {
    Dictionary<string, string> properties = new();
    if (project.Properties == null) {
      return properties;
    }

    foreach (XElement? property in project.Properties.Any) {
      properties[property.Name.LocalName] = property.Value;
    }

    return properties;
  }

  private string ReplaceProperties(string input,
                                   Dictionary<string, string> properties) {
    string v = input;
    try {
      v = Regex.Replace(
        input,
        @"\$\{(.+?)\}",
        m => properties[m.Groups[1].Value]
      );
    } catch {
      v = input;
    }

    return v;
  }


  private static T Parse<T>(Stream stream, bool ns = true) {
    T result = default;
    XmlSerializer serializer = new(typeof(T));
    using StreamReader sr = new(stream);
    result = (T)serializer.Deserialize(
      new XmlTextReader(sr) {
        Namespaces = ns
      }
    );
    return result;
  }

  private string GetGroup(Artifact artifact) {
    if (!artifact.config.ContainsKey("group")) {
      throw new MissingFieldException("Group is missing from artifact");
    }

    return artifact.config["group"].Replace(".", "/");
  }
}