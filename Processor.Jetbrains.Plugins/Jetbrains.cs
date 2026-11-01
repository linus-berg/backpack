// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Kernel.Exceptions;
using Core.Kernel.Models;
using Microsoft.Extensions.Logging;
using Processor.Jetbrains.Plugins.Models;
using RestSharp;

namespace Processor.Jetbrains.Plugins;

/// <summary>
///   Logic for processing JetBrains plugins: every update of a plugin on the
///   requested Marketplace channels, read page by page.
/// </summary>
public class Jetbrains : IJetbrains {
  /// <summary>
  ///   Artifact config key with the channels to mirror: a comma-separated
  ///   list such as <c>stable, eap</c>, or <c>all</c>. Empty means stable.
  /// </summary>
  public const string C_CHANNELS_KEY = "channels";

  private const string C_API_ = "https://plugins.jetbrains.com";

  /* The Marketplace returns at most 100 updates per page. */
  private const int C_PAGE_SIZE_ = 100;

  /* A safety net far beyond any real plugin (the busiest have a few
     thousand updates per channel). */
  private const int C_MAX_PAGES_ = 1000;

  private static readonly Regex S_NEXT_LINK_ =
    new(@"rel=""?next""?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

  private readonly RestClient client_ = new(
    new RestClientOptions(C_API_) { Timeout = TimeSpan.FromSeconds(60) }
  );

  private readonly ILogger<Jetbrains> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="Jetbrains" /> class.
  /// </summary>
  /// <param name="logger">Logger.</param>
  public Jetbrains(ILogger<Jetbrains> logger) {
    logger_ = logger;
  }

  /// <summary>
  ///   Processes the artifact to find JetBrains plugin updates.
  /// </summary>
  /// <param name="artifact">The artifact to process.</param>
  /// <returns>A task that represents the process operation, containing the updated artifact.</returns>
  public async Task<Artifact> ProcessArtifact(Artifact artifact) {
    string id = GetPluginId(artifact.id);
    foreach (string channel in await GetChannels(id, artifact.config)) {
      (List<JetbrainsPluginUpdate> updates, int pages) =
        await GetUpdates(id, channel);
      int added = AddVersions(artifact, updates);
      logger_.LogInformation(
        "Plugin {Plugin}, channel {Channel}: {Updates} updates on {Pages} pages, {Added} versions added",
        id,
        channel == "" ? "stable" : channel,
        updates.Count,
        pages,
        added
      );
    }

    return artifact;
  }

  /// <summary>
  ///   Adds one version per update. A version published on several channels
  ///   (as IdeaVim does) keeps the copy of the first channel, which is stable
  ///   whenever stable is requested.
  /// </summary>
  private static int AddVersions(Artifact artifact,
                                 List<JetbrainsPluginUpdate> updates) {
    int added = 0;
    foreach (JetbrainsPluginUpdate update in updates) {
      ArtifactVersion version = new() {
        version = update.version
      };
      version.AddFile("plugin", $"{C_API_}/files/{update.file}");
      if (artifact.AddVersion(version)) {
        added++;
      }
    }

    return added;
  }

  /// <summary>
  ///   The channels to read, stable first. The Marketplace calls the stable
  ///   channel "" and serves one channel per request.
  /// </summary>
  private async Task<List<string>> GetChannels(
    string id,
    IReadOnlyDictionary<string, string>? config) {
    string value = config != null &&
                   config.TryGetValue(C_CHANNELS_KEY, out string? configured)
                     ? configured
                     : "";
    List<string> names = value.Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries
                              )
                              .ToList();
    if (names.Count == 0) {
      return new List<string> { "" };
    }

    IEnumerable<string> channels =
      names.Any(n => n.Equals("all", StringComparison.OrdinalIgnoreCase))
        ? (await Get<List<string>>(
             id,
             new RestRequest($"/api/plugins/{id}/channels")
           )).Data ?? new List<string> { "" }
        : names.Select(
          n => n.Equals("stable", StringComparison.OrdinalIgnoreCase) ? "" : n
        );
    /* OrderBy is stable, so the other channels keep their order. */
    return channels.Distinct().OrderBy(c => c == "" ? 0 : 1).ToList();
  }

  /// <summary>
  ///   Every update on one channel, following the pages until the
  ///   Marketplace has no next page.
  /// </summary>
  private async Task<(List<JetbrainsPluginUpdate> updates, int pages)>
    GetUpdates(string id, string channel) {
    List<JetbrainsPluginUpdate> updates = new();
    HashSet<string> seen = new();
    for (int page = 1; page <= C_MAX_PAGES_; page++) {
      RestRequest request = new($"/api/plugins/{id}/updates");
      request.AddQueryParameter("channel", channel);
      request.AddQueryParameter("size", C_PAGE_SIZE_.ToString());
      request.AddQueryParameter("page", page.ToString());
      RestResponse<List<JetbrainsPluginUpdate>> response =
        await Get<List<JetbrainsPluginUpdate>>(id, request);
      List<JetbrainsPluginUpdate> batch =
        response.Data ?? new List<JetbrainsPluginUpdate>();

      /* Updates are newest first, so one published while we page pushes the
         rest down and repeats the last update of the previous page. */
      int fresh = 0;
      foreach (JetbrainsPluginUpdate update in batch) {
        if (seen.Add(update.id > 0 ? update.id.ToString() : update.file)) {
          updates.Add(update);
          fresh++;
        }
      }

      /* An empty page, or one with nothing new (an API that ignores the page
         parameter), ends the channel too. */
      if (fresh == 0 || !HasNextPage(response, batch.Count)) {
        return (updates, page);
      }
    }

    throw new ArtifactMetadataException(
      $"{id} has more than {C_MAX_PAGES_ * C_PAGE_SIZE_} updates on channel " +
      $"\"{channel}\"; stopped paging"
    );
  }

  /// <summary>
  ///   The Link header names the next page (<c>rel=next</c>). Without the
  ///   header, a non-empty page may be followed by more.
  /// </summary>
  private static bool HasNextPage(RestResponse response, int count) {
    string? link = response.GetHeaderValue("Link");
    return link != null ? S_NEXT_LINK_.IsMatch(link) : count > 0;
  }

  /// <summary>
  ///   Sends a request. Timeouts, network errors, rate limiting and server
  ///   errors are retried later (<see cref="ArtifactTimeoutException" />);
  ///   anything else fails the artifact, with the Marketplace's own message
  ///   where it gives one (e.g. "Can't find Plugin with id 123").
  /// </summary>
  private async Task<RestResponse<T>> Get<T>(string id, RestRequest request) {
    RestResponse<T> response = await client_.ExecuteAsync<T>(request);
    int status = (int)response.StatusCode;
    if (status == 0) {
      /* No HTTP response at all. */
      throw new ArtifactTimeoutException(
        response.ResponseStatus == ResponseStatus.TimedOut
          ? $"{id} timed out!"
          : $"{id}: the Marketplace is unreachable ({response.ErrorMessage})"
      );
    }

    if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500) {
      throw new ArtifactTimeoutException(
        $"{id}: the Marketplace answered {status}, retrying later"
      );
    }

    if (!response.IsSuccessStatusCode) {
      throw new ArtifactMetadataException(
        $"{id}: {MarketplaceMessage(response) ?? $"metadata error (HTTP {status})"}"
      );
    }

    if (response.ErrorException != null) {
      throw new ArtifactMetadataException(
        $"{id}: the Marketplace returned a response that couldn't be read"
      );
    }

    return response;
  }

  /// <summary>
  ///   The <c>message</c> of a Marketplace error body, if there is one.
  /// </summary>
  private static string? MarketplaceMessage(RestResponse response) {
    try {
      using JsonDocument document = JsonDocument.Parse(response.Content ?? "");
      return document.RootElement.ValueKind == JsonValueKind.Object &&
             document.RootElement.TryGetProperty("message", out JsonElement message) &&
             message.ValueKind == JsonValueKind.String
               ? message.GetString()
               : null;
    } catch (JsonException) {
      return null;
    }
  }

  private static string GetPluginId(string full_id) {
    return full_id.Split('-')[0];
  }
}
