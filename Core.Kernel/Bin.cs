// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using CliWrap;
using CliWrap.Builders;
using Microsoft.Extensions.Logging;

namespace Core.Kernel;

/// <summary>
///   Provides utility methods for executing external binary files.
/// </summary>
public static class Bin {
  /// <summary>
  ///   Executes an external binary with the specified arguments and logging.
  /// </summary>
  /// <param name="binary">The path or name of the binary to execute.</param>
  /// <param name="builder">An action to build the command-line arguments.</param>
  /// <param name="logger">The logger to use for capturing output and errors.</param>
  /// <param name="wd">The working directory in which to execute the binary.</param>
  /// <param name="success_code">The expected exit code indicating success.</param>
  /// <param name="token">A cancellation token to cancel the operation.</param>
  /// <param name="environment">
  ///   Extra environment variables for the process, merged over the current
  ///   environment. Use this for secrets rather than arguments, which are
  ///   visible in the process list.
  /// </param>
  /// <returns>
  ///   A task that represents the asynchronous operation, returning true if the binary executed successfully;
  ///   otherwise, false.
  /// </returns>
  public static async Task<bool> Execute(string binary,
                                         Action<ArgumentsBuilder> builder,
                                         ILogger logger,
                                         string wd = "",
                                         int success_code = 0,
                                         CancellationToken token =
                                           default,
                                         IReadOnlyDictionary<string, string?>?
                                           environment = null) {
    StringBuilder std_out = new();
    StringBuilder std_err = new();
    Command cmd = Cli.Wrap(binary)
                     .WithArguments(builder)
                     .WithWorkingDirectory(wd)
                     .WithStandardOutputPipe(
                       PipeTarget.ToStringBuilder(std_out)
                     )
                     .WithStandardErrorPipe(
                       PipeTarget.ToStringBuilder(std_err)
                     )
                     .WithValidation(CommandResultValidation.None);
    if (environment != null) {
      cmd = cmd.WithEnvironmentVariables(environment);
    }

    CommandResult? result = null;
    try {
      result = await cmd.ExecuteAsync(token);
    } catch (Exception e) {
      logger.LogError("{Error}", e.ToString());
    }

    logger.LogDebug("{StdOut}", std_out.ToString());
    bool success = result?.ExitCode == success_code;
    if (success) {
      logger.LogDebug("{StdErr}", std_err.ToString());
    } else {
      /* Surface why a command failed (e.g. git authentication errors)
         instead of hiding it at debug level. */
      logger.LogWarning(
        "{Binary} exited with {ExitCode}: {StdErr}",
        binary,
        result?.ExitCode.ToString() ?? "no result",
        std_err.ToString().Trim()
      );
    }

    return success;
  }
}