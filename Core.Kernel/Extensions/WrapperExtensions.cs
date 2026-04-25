// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using CliWrap;

namespace Core.Kernel.Extensions;

/// <summary>
///   Provides extension methods for <see cref="Command" /> to simplify binary execution and result capturing.
/// </summary>
public static class WrapperExtensions {
  /// <summary>
  ///   Executes a command and deserializes its standard output to the specified type.
  /// </summary>
  /// <typeparam name="T">The type to deserialize the output to.</typeparam>
  /// <param name="cmd">The command to execute.</param>
  /// <returns>The deserialized result of the command execution.</returns>
  /// <exception cref="ApplicationException">Thrown when the command execution fails.</exception>
  public static async Task<T?> ExecuteWithResult<T>(this Command cmd) {
    StringBuilder sb = new();
    StringBuilder sb_err = new();
    Command final_cmd =
      cmd |
      (PipeTarget.ToStringBuilder(sb),
       PipeTarget.ToStringBuilder(sb_err));
    try {
      await final_cmd.ExecuteAsync();
    } catch (Exception e) {
      sb_err.AppendLine(e.Message);
      throw new ApplicationException(sb_err.ToString());
    }

    return JsonSerializer.Deserialize<T>(sb.ToString());
  }

  /// <summary>
  ///   Executes a command and pipes its output directly to the console.
  /// </summary>
  /// <param name="cmd">The command to execute.</param>
  /// <returns>True if the command executed successfully (exit code 0); otherwise, false.</returns>
  public static async Task<bool> ExecuteToConsole(this Command cmd) {
    await using Stream stdout = Console.OpenStandardOutput();
    await using Stream stderr = Console.OpenStandardError();
    cmd |= (stdout, stderr);
    try {
      CommandResult result = await cmd.ExecuteAsync();
      return result.ExitCode == 0;
    } catch (Exception e) {
      return false;
    }
  }
}