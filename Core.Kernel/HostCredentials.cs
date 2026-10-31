// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Core.Kernel;

/// <summary>
///   A credential for one host, as written in the credentials file. The token
///   itself should come from <see cref="token_file" /> or <see cref="token_env" />
///   so it can be mounted as a secret; <see cref="token" /> is for local testing.
/// </summary>
public class HostCredential {
  /// <summary>
  ///   Username sent with the token. GitHub accepts any non-empty username
  ///   with a personal access token; use the account that owns the token.
  /// </summary>
  public string username { get; set; } = "x-access-token";

  /// <summary>
  ///   Path to a file containing the token. Re-read on every use, so a
  ///   rotated token is picked up without restarting the service.
  /// </summary>
  public string? token_file { get; set; }

  /// <summary>
  ///   Name of an environment variable containing the token.
  /// </summary>
  public string? token_env { get; set; }

  /// <summary>
  ///   The token itself. Prefer <see cref="token_file" /> outside local testing.
  /// </summary>
  public string? token { get; set; }
}

/// <summary>
///   Resolves credentials for remote hosts so authenticated sources can be
///   collected without putting secrets in artifact IDs, URLs or config, which
///   Backpack stores, logs and shows in the UI.
/// </summary>
/// <remarks>
///   Configured with <c>BP_CREDENTIALS_FILE</c>, a JSON object keyed by host
///   (or <c>host:port</c>):
///   <code>
///   {
///     "ghe.example.com": {
///       "username": "backpack-mirror",
///       "token_file": "/run/secrets/ghe-token"
///     }
///   }
///   </code>
/// </remarks>
public class HostCredentials {
  /// <summary>Environment variable holding the path to the credentials file.</summary>
  public const string C_FILE_VARIABLE = "BP_CREDENTIALS_FILE";

  private readonly Dictionary<string, HostCredential> credentials_;
  private readonly ILogger? logger_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="HostCredentials" /> class.
  /// </summary>
  /// <param name="credentials">Credentials keyed by host or host:port.</param>
  /// <param name="logger">Optional logger for configuration problems.</param>
  public HostCredentials(IDictionary<string, HostCredential> credentials,
                         ILogger? logger = null) {
    credentials_ = new Dictionary<string, HostCredential>(
      credentials,
      StringComparer.OrdinalIgnoreCase
    );
    logger_ = logger;
  }

  /// <summary>
  ///   Gets the hosts that have credentials configured.
  /// </summary>
  public IReadOnlyCollection<string> hosts => credentials_.Keys;

  /// <summary>
  ///   Loads credentials from the file named by <c>BP_CREDENTIALS_FILE</c>.
  ///   Returns an empty set when the variable is unset; throws when it is set
  ///   but the file is missing or malformed, so misconfiguration fails at
  ///   startup instead of as confusing authentication errors later.
  /// </summary>
  /// <param name="logger">Optional logger.</param>
  /// <returns>The configured credentials.</returns>
  public static HostCredentials FromEnvironment(ILogger? logger = null) {
    string? path = Environment.GetEnvironmentVariable(C_FILE_VARIABLE);
    if (string.IsNullOrWhiteSpace(path)) {
      return new HostCredentials(
        new Dictionary<string, HostCredential>(),
        logger
      );
    }

    if (!File.Exists(path)) {
      throw new FileNotFoundException(
        $"{C_FILE_VARIABLE} points to {path}, which does not exist."
      );
    }

    Dictionary<string, HostCredential>? parsed;
    try {
      parsed = JsonSerializer.Deserialize<Dictionary<string, HostCredential>>(
        File.ReadAllText(path)
      );
    } catch (JsonException e) {
      throw new InvalidDataException(
        $"{C_FILE_VARIABLE} ({path}) is not valid JSON: {e.Message}"
      );
    }

    HostCredentials credentials = new(
      parsed ?? new Dictionary<string, HostCredential>(),
      logger
    );
    logger?.LogInformation(
      "Loaded credentials for {Count} host(s): {Hosts}",
      credentials.hosts.Count,
      string.Join(", ", credentials.hosts)
    );
    return credentials;
  }

  /// <summary>
  ///   Finds the credential for a URI, matching <c>host:port</c> first and
  ///   then the bare host.
  /// </summary>
  /// <param name="uri">The remote URI.</param>
  /// <param name="credential">The matching credential, if any.</param>
  /// <returns>True when a credential is configured for the host.</returns>
  public bool TryGet(Uri uri, [NotNullWhen(true)] out HostCredential? credential) {
    if (credentials_.TryGetValue(uri.Authority, out credential)) {
      return true;
    }

    return credentials_.TryGetValue(uri.Host, out credential);
  }

  /// <summary>
  ///   Builds an HTTP Basic <c>Authorization</c> header value for a URI.
  /// </summary>
  /// <param name="uri">The remote URI.</param>
  /// <param name="header_value">
  ///   The header value (<c>Basic …</c>), or null when no usable credential exists.
  /// </param>
  /// <returns>True when a header value was produced.</returns>
  public bool TryGetBasicAuthorization(Uri uri,
                                       [NotNullWhen(true)] out string?
                                         header_value) {
    header_value = null;
    if (!TryGet(uri, out HostCredential? credential)) {
      return false;
    }

    string? token = ReadToken(uri.Host, credential);
    if (string.IsNullOrEmpty(token)) {
      return false;
    }

    string pair = $"{credential.username}:{token}";
    header_value =
      $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(pair))}";
    return true;
  }

  /// <summary>
  ///   Reads the token for a credential. Never logs the token itself.
  /// </summary>
  private string? ReadToken(string host, HostCredential credential) {
    if (!string.IsNullOrEmpty(credential.token_file)) {
      try {
        return File.ReadAllText(credential.token_file).Trim();
      } catch (Exception e) {
        logger_?.LogError(
          "Could not read the token file for {Host} ({TokenFile}): {Error}",
          host,
          credential.token_file,
          e.Message
        );
        return null;
      }
    }

    if (!string.IsNullOrEmpty(credential.token_env)) {
      string? value = Environment.GetEnvironmentVariable(credential.token_env);
      if (string.IsNullOrEmpty(value)) {
        logger_?.LogError(
          "The token variable {TokenEnv} for {Host} is not set",
          credential.token_env,
          host
        );
      }

      return value?.Trim();
    }

    if (!string.IsNullOrEmpty(credential.token)) {
      return credential.token.Trim();
    }

    logger_?.LogError(
      "Credentials for {Host} have no token, token_file or token_env",
      host
    );
    return null;
  }
}
