// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel;

namespace Collector.Git;

/// <summary>
///   Builds the environment for git network commands (clone/fetch).
/// </summary>
public static class GitAuthentication {
  /// <summary>
  ///   Returns environment variables for running git against <paramref name="remote" />.
  /// </summary>
  /// <remarks>
  ///   When credentials exist for the remote's host, the token is passed as an
  ///   HTTP header through git's environment-only configuration
  ///   (<c>GIT_CONFIG_COUNT/KEY/VALUE</c>, git 2.31+). That way it is never
  ///   part of the remote URL (which Backpack logs and git writes into the
  ///   mirror's config), never on the command line (visible in the process
  ///   list), and only sent to that exact scheme, host and port.
  ///   <c>GIT_TERMINAL_PROMPT=0</c> makes git fail immediately instead of
  ///   waiting for a password when a repository needs credentials.
  /// </remarks>
  /// <param name="remote">The URL git will fetch from.</param>
  /// <param name="credentials">Configured host credentials.</param>
  /// <param name="has_credentials">Whether credentials were applied.</param>
  /// <returns>Environment variables to merge into the git process.</returns>
  public static IReadOnlyDictionary<string, string?> BuildEnvironment(
    Uri remote, HostCredentials credentials, out bool has_credentials) {
    Dictionary<string, string?> environment = new() {
      ["GIT_TERMINAL_PROMPT"] = "0"
    };

    has_credentials =
      credentials.TryGetBasicAuthorization(remote, out string? header);
    if (has_credentials) {
      string scope = $"{remote.Scheme}://{remote.Authority}/";
      environment["GIT_CONFIG_COUNT"] = "1";
      environment["GIT_CONFIG_KEY_0"] = $"http.{scope}.extraheader";
      environment["GIT_CONFIG_VALUE_0"] = $"Authorization: {header}";
    }

    return environment;
  }
}
