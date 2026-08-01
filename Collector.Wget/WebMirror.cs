// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Parser;
using Collector.Kernel;

namespace Collector.Wget;

/// <summary>
///   Provides native C# website mirroring functionality, replacing the external wget binary.
///   Implements recursive crawling with link conversion, page requisite downloading,
///   and extension adjustment — equivalent to: wget --mirror -k -p -E --no-parent.
/// </summary>
public class WebMirror {
  /// <summary>
  ///   Maximum number of concurrent HTTP requests during crawling.
  /// </summary>
  private const int MAX_CONCURRENCY = 8;

  /// <summary>
  ///   Maximum crawl depth to prevent runaway recursion on malformed sites.
  /// </summary>
  private const int MAX_DEPTH = 50;

  /// <summary>
  ///   Default timeout per individual HTTP request.
  /// </summary>
  private static readonly TimeSpan S_REQUEST_TIMEOUT =
    TimeSpan.FromSeconds(60);

  /// <summary>
  ///   Content types recognized as HTML documents eligible for link extraction.
  /// </summary>
  private static readonly HashSet<string> S_HTML_CONTENT_TYPES = new(
    StringComparer.OrdinalIgnoreCase
  ) {
    "text/html",
    "application/xhtml+xml"
  };

  /// <summary>
  ///   File extensions recognized as HTML documents by their URL path.
  /// </summary>
  private static readonly HashSet<string> S_HTML_EXTENSIONS = new(
    StringComparer.OrdinalIgnoreCase
  ) {
    ".html",
    ".htm",
    ".xhtml",
    ".asp",
    ".aspx",
    ".php",
    ".jsp",
    ".shtml"
  };

  /// <summary>
  ///   File extensions recognized as CSS stylesheets for @import / url() extraction.
  /// </summary>
  private static readonly HashSet<string> S_CSS_EXTENSIONS = new(
    StringComparer.OrdinalIgnoreCase
  ) {
    ".css"
  };

  private readonly FileSystem fs_;
  private readonly HttpClient http_client_;
  private readonly ILogger<WebMirror> logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="WebMirror" /> class.
  /// </summary>
  /// <param name="logger">The logger.</param>
  /// <param name="fs">The file system abstraction for S3/Minio storage.</param>
  /// <param name="http_client_factory">The HTTP client factory.</param>
  public WebMirror(ILogger<WebMirror> logger, FileSystem fs,
                   IHttpClientFactory http_client_factory) {
    logger_ = logger;
    fs_ = fs;
    http_client_ = http_client_factory.CreateClient("mirror-client");
  }

  /// <summary>
  ///   Mirrors a remote website starting from the given URL. Downloads all pages
  ///   and their requisites (CSS, JS, images) recursively, converts links to
  ///   local paths, and stores everything in S3 via the FileSystem abstraction.
  /// </summary>
  /// <param name="remote">The root URL to mirror.</param>
  /// <param name="token">Cancellation token.</param>
  /// <returns>True if the mirroring completed successfully; otherwise, false.</returns>
  public async Task<bool> Mirror(string remote,
                                 CancellationToken token = default) {
    Uri base_uri;
    try {
      base_uri = new Uri(remote);
    } catch (UriFormatException ex) {
      logger_.LogError("Invalid URL '{Url}': {Error}", remote, ex.Message);
      return false;
    }

    logger_.LogInformation("Starting mirror of {Url}", remote);

    MirrorContext ctx = new(base_uri);
    SemaphoreSlim throttle = new(MAX_CONCURRENCY, MAX_CONCURRENCY);

    try {
      // Phase 1: Crawl and download all resources
      await CrawlAsync(ctx, base_uri, 0, true, throttle, token);

      // Phase 2: Rewrite links in HTML/CSS files and upload to S3
      await RewriteAndUploadAsync(ctx, token);

      logger_.LogInformation(
        "Mirror complete: {Count} resources collected from {Url}",
        ctx.downloaded_resources.Count,
        remote
      );
      return true;
    } catch (OperationCanceledException) {
      logger_.LogWarning("Mirror of {Url} was cancelled", remote);
      return false;
    } catch (Exception ex) {
      logger_.LogError(ex, "Mirror of {Url} failed", remote);
      return false;
    }
  }

  /// <summary>
  ///   Recursively crawls a URL, downloading the resource and discovering linked resources.
  /// </summary>
  /// <param name="ctx">The mirror context tracking visited URLs and downloaded content.</param>
  /// <param name="url">The URL to crawl.</param>
  /// <param name="depth">Current recursion depth.</param>
  /// <param name="is_page">Whether this URL is expected to be an HTML page (eligible for link extraction).</param>
  /// <param name="throttle">Semaphore for concurrency control.</param>
  /// <param name="token">Cancellation token.</param>
  private async Task CrawlAsync(MirrorContext ctx, Uri url, int depth,
                                bool is_page, SemaphoreSlim throttle,
                                CancellationToken token) {
    if (depth > MAX_DEPTH) {
      return;
    }

    // Normalize and validate the URL
    Uri normalized = NormalizeUri(url);
    string url_key = normalized.AbsoluteUri;

    // Skip if already visited
    if (!ctx.visited.TryAdd(url_key, true)) {
      return;
    }

    // Enforce --no-parent: only crawl URLs under the base path
    if (!IsWithinScope(ctx.base_uri, normalized)) {
      return;
    }

    // Skip non-HTTP(S) schemes
    if (normalized.Scheme != "http" && normalized.Scheme != "https") {
      return;
    }

    await throttle.WaitAsync(token);
    try {
      HttpResponseMessage? response =
        await FetchAsync(normalized, token);
      if (response == null) {
        return;
      }

      string? content_type =
        response.Content.Headers.ContentType?.MediaType;
      byte[] body = await response.Content.ReadAsByteArrayAsync(token);
      string local_path = UriToLocalPath(ctx.base_uri, normalized,
        content_type);

      DownloadedResource resource = new() {
        uri = normalized,
        local_path = local_path,
        content_type = content_type ?? "application/octet-stream",
        body = body,
        is_html = IsHtmlContent(content_type, normalized),
        is_css = IsCssContent(content_type, normalized)
      };

      ctx.downloaded_resources.TryAdd(url_key, resource);

      logger_.LogDebug(
        "Downloaded [{Depth}] {Url} -> {Path} ({Size} bytes)",
        depth,
        normalized.AbsoluteUri,
        local_path,
        body.Length
      );

      // Extract and recursively crawl linked resources
      List<Uri> discovered_links = new();

      if (resource.is_html) {
        discovered_links.AddRange(
          await ExtractHtmlLinksAsync(body, normalized, token)
        );
      } else if (resource.is_css) {
        discovered_links.AddRange(
          ExtractCssLinks(body, normalized)
        );
      }

      // Crawl discovered links in parallel
      List<Task> child_tasks = new();
      foreach (Uri link in discovered_links) {
        bool link_is_page = IsLikelyHtmlUrl(link);
        child_tasks.Add(
          CrawlAsync(ctx, link, depth + 1, link_is_page, throttle, token)
        );
      }

      await Task.WhenAll(child_tasks);
    } finally {
      throttle.Release();
    }
  }

  /// <summary>
  ///   Fetches a URL, returning the response or null on failure.
  /// </summary>
  /// <param name="url">The URL to fetch.</param>
  /// <param name="token">Cancellation token.</param>
  /// <returns>The HTTP response, or null if the request failed.</returns>
  private async Task<HttpResponseMessage?> FetchAsync(Uri url,
    CancellationToken token) {
    try {
      using CancellationTokenSource timeout_cts =
        CancellationTokenSource.CreateLinkedTokenSource(token);
      timeout_cts.CancelAfter(S_REQUEST_TIMEOUT);

      HttpResponseMessage response = await http_client_.GetAsync(
        url,
        HttpCompletionOption.ResponseHeadersRead,
        timeout_cts.Token
      );

      if (!response.IsSuccessStatusCode) {
        logger_.LogWarning(
          "HTTP {StatusCode} for {Url}",
          (int)response.StatusCode,
          url.AbsoluteUri
        );
        return null;
      }

      return response;
    } catch (TaskCanceledException) when (!token.IsCancellationRequested) {
      logger_.LogWarning("Timeout fetching {Url}", url.AbsoluteUri);
      return null;
    } catch (HttpRequestException ex) {
      logger_.LogWarning(
        "Failed to fetch {Url}: {Error}",
        url.AbsoluteUri,
        ex.Message
      );
      return null;
    }
  }

  /// <summary>
  ///   Parses an HTML document and extracts all linked resource URLs,
  ///   including page links (a[href]), stylesheets (link[href]),
  ///   scripts (script[src]), images (img[src]), and other embedded resources.
  /// </summary>
  /// <param name="html_bytes">The raw HTML bytes.</param>
  /// <param name="page_url">The URL of the page (used for resolving relative links).</param>
  /// <param name="token">Cancellation token.</param>
  /// <returns>A list of absolute URIs discovered in the document.</returns>
  private async Task<List<Uri>> ExtractHtmlLinksAsync(byte[] html_bytes,
    Uri page_url, CancellationToken token) {
    List<Uri> links = new();

    try {
      string html = Encoding.UTF8.GetString(html_bytes);
      IBrowsingContext browser_context = BrowsingContext.New(
        AngleSharp.Configuration.Default
      );
      HtmlParser parser = new();
      IDocument document = await parser.ParseDocumentAsync(html, token);

      // Page links (recursive crawl targets)
      ExtractAttributeLinks(document, "a", "href", page_url, links);

      // Page requisites (-p flag equivalent)
      ExtractAttributeLinks(document, "link", "href", page_url, links);
      ExtractAttributeLinks(document, "script", "src", page_url, links);
      ExtractAttributeLinks(document, "img", "src", page_url, links);
      ExtractAttributeLinks(document, "img", "srcset", page_url, links,
        true);
      ExtractAttributeLinks(document, "source", "src", page_url, links);
      ExtractAttributeLinks(document, "source", "srcset", page_url, links,
        true);
      ExtractAttributeLinks(document, "video", "src", page_url, links);
      ExtractAttributeLinks(document, "video", "poster", page_url, links);
      ExtractAttributeLinks(document, "audio", "src", page_url, links);
      ExtractAttributeLinks(document, "embed", "src", page_url, links);
      ExtractAttributeLinks(document, "object", "data", page_url, links);
      ExtractAttributeLinks(document, "iframe", "src", page_url, links);

      // Inline style url() references
      IHtmlCollection<IElement> styled_elements =
        document.QuerySelectorAll("[style]");
      foreach (IElement element in styled_elements) {
        string? style = element.GetAttribute("style");
        if (style != null) {
          links.AddRange(ExtractCssUrlReferences(style, page_url));
        }
      }

      // <style> blocks
      IHtmlCollection<IElement> style_elements =
        document.QuerySelectorAll("style");
      foreach (IElement style_element in style_elements) {
        string css_text = style_element.TextContent;
        links.AddRange(ExtractCssUrlReferences(css_text, page_url));
      }
    } catch (Exception ex) {
      logger_.LogWarning(
        "Failed to parse HTML from {Url}: {Error}",
        page_url.AbsoluteUri,
        ex.Message
      );
    }

    return links;
  }

  /// <summary>
  ///   Extracts links from a specific attribute of elements matching the given selector.
  /// </summary>
  /// <param name="document">The parsed HTML document.</param>
  /// <param name="tag">The HTML tag name to select.</param>
  /// <param name="attribute">The attribute containing the URL.</param>
  /// <param name="base_url">The base URL for resolving relative links.</param>
  /// <param name="links">The output list to add discovered URIs to.</param>
  /// <param name="is_srcset">Whether to parse the attribute as a srcset value.</param>
  private void ExtractAttributeLinks(IDocument document, string tag,
                                     string attribute, Uri base_url,
                                     List<Uri> links,
                                     bool is_srcset = false) {
    IHtmlCollection<IElement> elements =
      document.QuerySelectorAll($"{tag}[{attribute}]");

    foreach (IElement element in elements) {
      string? value = element.GetAttribute(attribute);
      if (string.IsNullOrWhiteSpace(value)) {
        continue;
      }

      if (is_srcset) {
        // srcset contains comma-separated "url descriptor" pairs
        string[] entries = value.Split(',');
        foreach (string entry in entries) {
          string url_part = entry.Trim().Split(' ')[0];
          TryResolveAndAdd(url_part, base_url, links);
        }
      } else {
        TryResolveAndAdd(value, base_url, links);
      }
    }
  }

  /// <summary>
  ///   Extracts URL references from CSS content (both url() and @import directives).
  /// </summary>
  /// <param name="css_bytes">The raw CSS bytes.</param>
  /// <param name="css_url">The URL of the CSS file (used for resolving relative links).</param>
  /// <returns>A list of absolute URIs discovered in the CSS.</returns>
  private List<Uri> ExtractCssLinks(byte[] css_bytes, Uri css_url) {
    string css = Encoding.UTF8.GetString(css_bytes);
    return ExtractCssUrlReferences(css, css_url);
  }

  /// <summary>
  ///   Extracts url() and @import references from CSS text.
  /// </summary>
  /// <param name="css">The CSS text to parse.</param>
  /// <param name="base_url">The base URL for resolving relative references.</param>
  /// <returns>A list of resolved URIs.</returns>
  private List<Uri> ExtractCssUrlReferences(string css, Uri base_url) {
    List<Uri> links = new();

    // Match url("..."), url('...'), url(...)
    MatchCollection url_matches = Regex.Matches(
      css,
      @"url\(\s*[""']?([^""')]+)[""']?\s*\)",
      RegexOptions.IgnoreCase
    );
    foreach (Match match in url_matches) {
      string url_value = match.Groups[1].Value.Trim();
      // Skip data URIs
      if (!url_value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
        TryResolveAndAdd(url_value, base_url, links);
      }
    }

    // Match @import "..." and @import url(...)
    MatchCollection import_matches = Regex.Matches(
      css,
      @"@import\s+[""']([^""']+)[""']",
      RegexOptions.IgnoreCase
    );
    foreach (Match match in import_matches) {
      TryResolveAndAdd(match.Groups[1].Value.Trim(), base_url, links);
    }

    return links;
  }

  /// <summary>
  ///   After all resources are downloaded, rewrites links in HTML and CSS content
  ///   to point to local paths, then uploads everything to S3 storage.
  /// </summary>
  /// <param name="ctx">The mirror context containing all downloaded resources.</param>
  /// <param name="token">Cancellation token.</param>
  private async Task RewriteAndUploadAsync(MirrorContext ctx,
                                           CancellationToken token) {
    // Build a URL-to-local-path mapping for link rewriting
    Dictionary<string, string> url_to_local = new(
      StringComparer.OrdinalIgnoreCase
    );
    foreach (KeyValuePair<string, DownloadedResource> kvp in
             ctx.downloaded_resources) {
      url_to_local[kvp.Key] = kvp.Value.local_path;
    }

    foreach (KeyValuePair<string, DownloadedResource> kvp in
             ctx.downloaded_resources) {
      token.ThrowIfCancellationRequested();

      DownloadedResource resource = kvp.Value;
      byte[] final_bytes;

      if (resource.is_html) {
        string html = Encoding.UTF8.GetString(resource.body);
        string rewritten = RewriteHtmlLinks(html, resource.uri,
          url_to_local, resource.local_path);
        final_bytes = Encoding.UTF8.GetBytes(rewritten);
      } else if (resource.is_css) {
        string css = Encoding.UTF8.GetString(resource.body);
        string rewritten = RewriteCssLinks(css, resource.uri,
          url_to_local, resource.local_path);
        final_bytes = Encoding.UTF8.GetBytes(rewritten);
      } else {
        final_bytes = resource.body;
      }

      // Upload to S3 via the FileSystem abstraction
      string storage_path = GetStoragePath(ctx.base_uri, resource.local_path);
      using MemoryStream stream = new(final_bytes);
      bool success = await fs_.PutFile(storage_path, stream);

      if (success) {
        logger_.LogDebug(
          "Uploaded {LocalPath} -> s3://{StoragePath} ({Size} bytes)",
          resource.local_path,
          storage_path,
          final_bytes.Length
        );
      } else {
        logger_.LogWarning(
          "Failed to upload {LocalPath} to S3",
          resource.local_path
        );
      }
    }
  }

  /// <summary>
  ///   Rewrites absolute URLs in an HTML document to relative local paths.
  ///   This is the equivalent of wget's -k (--convert-links) flag.
  /// </summary>
  /// <param name="html">The HTML source.</param>
  /// <param name="page_url">The URL of this page.</param>
  /// <param name="url_to_local">Mapping of absolute URL to local path.</param>
  /// <param name="current_local_path">The local path of this page.</param>
  /// <returns>The HTML with rewritten links.</returns>
  private string RewriteHtmlLinks(string html, Uri page_url,
                                  Dictionary<string, string> url_to_local,
                                  string current_local_path) {
    // Replace href and src attributes pointing to downloaded resources
    string result = Regex.Replace(
      html,
      @"((?:href|src|action|poster|data)\s*=\s*[""'])([^""']+)([""'])",
      match => {
        string prefix = match.Groups[1].Value;
        string url_value = match.Groups[2].Value;
        string suffix = match.Groups[3].Value;

        string? resolved = ResolveToLocalPath(url_value, page_url,
          url_to_local, current_local_path);
        return resolved != null
          ? $"{prefix}{resolved}{suffix}"
          : match.Value;
      },
      RegexOptions.IgnoreCase
    );

    // Rewrite url() references in inline styles and <style> blocks
    result = RewriteCssUrlInText(result, page_url, url_to_local,
      current_local_path);

    // Rewrite srcset attributes
    result = Regex.Replace(
      result,
      @"(srcset\s*=\s*[""'])([^""']+)([""'])",
      match => {
        string prefix = match.Groups[1].Value;
        string srcset = match.Groups[2].Value;
        string suffix = match.Groups[3].Value;

        string[] entries = srcset.Split(',');
        List<string> rewritten_entries = new();
        foreach (string entry in entries) {
          string trimmed = entry.Trim();
          string[] parts = trimmed.Split(' ', 2);
          string url_part = parts[0];
          string descriptor = parts.Length > 1 ? " " + parts[1] : "";

          string? resolved = ResolveToLocalPath(url_part, page_url,
            url_to_local, current_local_path);
          rewritten_entries.Add(
            (resolved ?? url_part) + descriptor
          );
        }

        return $"{prefix}{string.Join(", ", rewritten_entries)}{suffix}";
      },
      RegexOptions.IgnoreCase
    );

    return result;
  }

  /// <summary>
  ///   Rewrites url() references in CSS content to point to local paths.
  /// </summary>
  /// <param name="css">The CSS source text.</param>
  /// <param name="css_url">The URL of the CSS file.</param>
  /// <param name="url_to_local">Mapping of absolute URL to local path.</param>
  /// <param name="current_local_path">The local path of this CSS file.</param>
  /// <returns>The CSS with rewritten url() references.</returns>
  private string RewriteCssLinks(string css, Uri css_url,
                                 Dictionary<string, string> url_to_local,
                                 string current_local_path) {
    return RewriteCssUrlInText(css, css_url, url_to_local,
      current_local_path);
  }

  /// <summary>
  ///   Rewrites url() function calls within any text (CSS or inline styles).
  /// </summary>
  /// <param name="text">The text containing url() references.</param>
  /// <param name="base_url">The base URL for resolving relative references.</param>
  /// <param name="url_to_local">Mapping of absolute URL to local path.</param>
  /// <param name="current_local_path">The local path of the current file.</param>
  /// <returns>The text with rewritten url() references.</returns>
  private string RewriteCssUrlInText(string text, Uri base_url,
                                     Dictionary<string, string> url_to_local,
                                     string current_local_path) {
    return Regex.Replace(
      text,
      @"url\(\s*([""']?)([^""')]+)\1\s*\)",
      match => {
        string quote = match.Groups[1].Value;
        string url_value = match.Groups[2].Value;

        if (url_value.StartsWith("data:",
              StringComparison.OrdinalIgnoreCase)) {
          return match.Value;
        }

        string? resolved = ResolveToLocalPath(url_value, base_url,
          url_to_local, current_local_path);
        return resolved != null
          ? $"url({quote}{resolved}{quote})"
          : match.Value;
      },
      RegexOptions.IgnoreCase
    );
  }

  /// <summary>
  ///   Resolves a URL reference to a relative local path if the target was downloaded.
  /// </summary>
  /// <param name="url_value">The URL value from the attribute/CSS.</param>
  /// <param name="base_url">The base URL of the containing document.</param>
  /// <param name="url_to_local">Mapping of absolute URL to local path.</param>
  /// <param name="current_local_path">The local path of the containing document.</param>
  /// <returns>A relative path to the local file, or null if not found.</returns>
  private string? ResolveToLocalPath(string url_value, Uri base_url,
                                     Dictionary<string, string> url_to_local,
                                     string current_local_path) {
    // Strip fragment identifiers
    int hash_index = url_value.IndexOf('#');
    string fragment = "";
    if (hash_index >= 0) {
      fragment = url_value.Substring(hash_index);
      url_value = url_value.Substring(0, hash_index);
    }

    if (string.IsNullOrWhiteSpace(url_value)) {
      return null;
    }

    Uri? resolved;
    try {
      resolved = new Uri(base_url, url_value);
    } catch {
      return null;
    }

    string resolved_key = resolved.AbsoluteUri;
    if (url_to_local.TryGetValue(resolved_key, out string? target_path)) {
      string relative = ComputeRelativePath(current_local_path, target_path);
      return relative + fragment;
    }

    return null;
  }

  /// <summary>
  ///   Computes a relative path from one local path to another.
  /// </summary>
  /// <param name="from_path">The source file's local path.</param>
  /// <param name="to_path">The target file's local path.</param>
  /// <returns>A relative path string.</returns>
  private static string ComputeRelativePath(string from_path,
                                            string to_path) {
    string[] from_parts = from_path.Split('/');
    string[] to_parts = to_path.Split('/');

    // Find common prefix length (directories only, not the filename)
    int from_dir_count = from_parts.Length - 1;
    int to_dir_count = to_parts.Length - 1;
    int common = 0;

    int max_common = Math.Min(from_dir_count, to_dir_count);
    for (int i = 0; i < max_common; i++) {
      if (string.Equals(from_parts[i], to_parts[i],
            StringComparison.Ordinal)) {
        common++;
      } else {
        break;
      }
    }

    // Number of "../" needed to go up from the from_path's directory
    int ups = from_dir_count - common;
    StringBuilder sb = new();
    for (int i = 0; i < ups; i++) {
      sb.Append("../");
    }

    // Append the remainder of the to_path
    for (int i = common; i < to_parts.Length; i++) {
      if (i > common) {
        sb.Append('/');
      }

      sb.Append(to_parts[i]);
    }

    string result = sb.ToString();
    return string.IsNullOrEmpty(result) ? "./" : result;
  }

  /// <summary>
  ///   Converts a remote URI to a local file path, mirroring wget's directory structure.
  ///   For example: https://example.com/docs/page → example.com/docs/page.html
  /// </summary>
  /// <param name="base_uri">The base URI of the mirror operation.</param>
  /// <param name="resource_uri">The URI of the resource being saved.</param>
  /// <param name="content_type">The Content-Type header from the HTTP response.</param>
  /// <returns>A local file path string.</returns>
  private static string UriToLocalPath(Uri base_uri, Uri resource_uri,
                                       string? content_type) {
    string host = resource_uri.Host;
    string path = Uri.UnescapeDataString(resource_uri.AbsolutePath);

    // Remove leading slash
    if (path.StartsWith("/")) {
      path = path.Substring(1);
    }

    // If path is empty or ends with /, treat as index
    if (string.IsNullOrEmpty(path) || path.EndsWith("/")) {
      path += "index.html";
    }

    // -E flag: Adjust extension for HTML content served without .html extension
    string extension = Path.GetExtension(path);
    if (IsHtmlContent(content_type, resource_uri) &&
        !S_HTML_EXTENSIONS.Contains(extension)) {
      path += ".html";
    }

    // Include query string in filename to differentiate dynamic pages
    string query = resource_uri.Query;
    if (!string.IsNullOrEmpty(query)) {
      // Replace query separators with safe filename characters
      string safe_query = query.Replace("?", "@").Replace("&", "@")
        .Replace("=", "_");
      string dir = Path.GetDirectoryName(path) ?? "";
      string name = Path.GetFileNameWithoutExtension(path);
      string ext = Path.GetExtension(path);
      path = Path.Combine(dir, name + safe_query + ext);
    }

    // Sanitize path segments
    path = SanitizePath(path);

    return $"{host}/{path}";
  }

  /// <summary>
  ///   Constructs the S3 storage path for a downloaded resource.
  /// </summary>
  /// <param name="base_uri">The base URI of the mirror.</param>
  /// <param name="local_path">The local path of the resource.</param>
  /// <returns>The S3 storage path including the wget module prefix.</returns>
  private static string GetStoragePath(Uri base_uri, string local_path) {
    return Path.Join("wget", local_path);
  }

  /// <summary>
  ///   Normalizes a URI by removing the fragment and ensuring consistent formatting.
  /// </summary>
  /// <param name="uri">The URI to normalize.</param>
  /// <returns>A normalized URI without fragment.</returns>
  private static Uri NormalizeUri(Uri uri) {
    UriBuilder builder = new(uri) {
      Fragment = ""
    };
    return builder.Uri;
  }

  /// <summary>
  ///   Checks whether a URL is within the --no-parent scope (same host,
  ///   and the path starts with the base path).
  /// </summary>
  /// <param name="base_uri">The base URI defining the scope.</param>
  /// <param name="candidate">The candidate URI to check.</param>
  /// <returns>True if the candidate is within scope.</returns>
  private static bool IsWithinScope(Uri base_uri, Uri candidate) {
    if (!string.Equals(base_uri.Host, candidate.Host,
          StringComparison.OrdinalIgnoreCase)) {
      return false;
    }

    // Allow different schemes (http/https) on the same host
    string base_path = base_uri.AbsolutePath;

    // Get the directory component of the base path
    if (!base_path.EndsWith("/")) {
      int last_slash = base_path.LastIndexOf('/');
      base_path = last_slash >= 0
        ? base_path.Substring(0, last_slash + 1)
        : "/";
    }

    return candidate.AbsolutePath.StartsWith(base_path,
      StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  ///   Determines whether a content type indicates HTML content.
  /// </summary>
  /// <param name="content_type">The Content-Type header value.</param>
  /// <param name="uri">The URI (used for extension-based fallback).</param>
  /// <returns>True if the content is HTML.</returns>
  private static bool IsHtmlContent(string? content_type, Uri uri) {
    if (content_type != null &&
        S_HTML_CONTENT_TYPES.Contains(content_type)) {
      return true;
    }

    // Fallback: check the URL extension
    string ext = Path.GetExtension(uri.AbsolutePath);
    return S_HTML_EXTENSIONS.Contains(ext) || string.IsNullOrEmpty(ext);
  }

  /// <summary>
  ///   Determines whether a content type indicates CSS content.
  /// </summary>
  /// <param name="content_type">The Content-Type header value.</param>
  /// <param name="uri">The URI (used for extension-based fallback).</param>
  /// <returns>True if the content is CSS.</returns>
  private static bool IsCssContent(string? content_type, Uri uri) {
    if (string.Equals(content_type, "text/css",
          StringComparison.OrdinalIgnoreCase)) {
      return true;
    }

    string ext = Path.GetExtension(uri.AbsolutePath);
    return S_CSS_EXTENSIONS.Contains(ext);
  }

  /// <summary>
  ///   Heuristically determines whether a URL is likely to be an HTML page.
  /// </summary>
  /// <param name="uri">The URI to check.</param>
  /// <returns>True if the URL likely points to an HTML page.</returns>
  private static bool IsLikelyHtmlUrl(Uri uri) {
    string ext = Path.GetExtension(uri.AbsolutePath);
    if (string.IsNullOrEmpty(ext)) {
      return true; // No extension usually means a page
    }

    return S_HTML_EXTENSIONS.Contains(ext);
  }

  /// <summary>
  ///   Attempts to resolve a relative URL against a base URL and add it to the links list.
  /// </summary>
  /// <param name="url_value">The URL string to resolve.</param>
  /// <param name="base_url">The base URL for resolution.</param>
  /// <param name="links">The list to add the resolved URI to.</param>
  private static void TryResolveAndAdd(string url_value, Uri base_url,
                                       List<Uri> links) {
    if (string.IsNullOrWhiteSpace(url_value)) {
      return;
    }

    // Skip javascript:, mailto:, tel:, data: URIs
    if (url_value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
        url_value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        url_value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
        url_value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
        url_value.StartsWith("#")) {
      return;
    }

    try {
      Uri resolved = new(base_url, url_value);
      links.Add(resolved);
    } catch (UriFormatException) {
      // Malformed URL, skip
    }
  }

  /// <summary>
  ///   Sanitizes a file path by removing or replacing characters that are invalid on disk.
  /// </summary>
  /// <param name="path">The path to sanitize.</param>
  /// <returns>A sanitized path string.</returns>
  private static string SanitizePath(string path) {
    // Replace backslashes with forward slashes
    path = path.Replace('\\', '/');

    // Remove double slashes
    while (path.Contains("//")) {
      path = path.Replace("//", "/");
    }

    // Remove leading/trailing slashes from individual segments
    // but preserve the overall structure
    string[] segments = path.Split('/');
    for (int i = 0; i < segments.Length; i++) {
      // Remove characters invalid in filenames
      segments[i] = Regex.Replace(segments[i], @"[<>:""|?*]", "_");
    }

    return string.Join("/", segments.Where(s => !string.IsNullOrEmpty(s)));
  }

  /// <summary>
  ///   Holds the state for a single mirroring operation including visited URLs
  ///   and downloaded resources.
  /// </summary>
  private class MirrorContext {
    /// <summary>
    ///   The root URI that defines the scope of this mirror operation.
    /// </summary>
    public readonly Uri base_uri;

    /// <summary>
    ///   Thread-safe dictionary of all downloaded resources keyed by their absolute URL.
    /// </summary>
    public readonly ConcurrentDictionary<string, DownloadedResource>
      downloaded_resources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   Thread-safe set of visited URLs to prevent revisiting.
    /// </summary>
    public readonly ConcurrentDictionary<string, bool> visited =
      new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   Initializes a new instance of the <see cref="MirrorContext" /> class.
    /// </summary>
    /// <param name="base_uri">The root URI for this mirror operation.</param>
    public MirrorContext(Uri base_uri) {
      this.base_uri = base_uri;
    }
  }

  /// <summary>
  ///   Represents a single downloaded resource with its metadata and content.
  /// </summary>
  private class DownloadedResource {
    /// <summary>
    ///   The raw bytes of the downloaded content.
    /// </summary>
    public required byte[] body;

    /// <summary>
    ///   The Content-Type of the resource.
    /// </summary>
    public required string content_type;

    /// <summary>
    ///   Whether this resource is a CSS stylesheet.
    /// </summary>
    public required bool is_css;

    /// <summary>
    ///   Whether this resource is an HTML document.
    /// </summary>
    public required bool is_html;

    /// <summary>
    ///   The local file path where this resource will be stored.
    /// </summary>
    public required string local_path;

    /// <summary>
    ///   The original URI of this resource.
    /// </summary>
    public required Uri uri;
  }
}
