// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Collector.Kernel.Storage.Minio;

/// <summary>
///   Builder for Minio connection strings.
/// </summary>
public class MinioConnectionBuilder {
  /// <summary>
  ///   Initializes a new instance of the <see cref="MinioConnectionBuilder" /> class.
  /// </summary>
  public MinioConnectionBuilder() {
  }

  /// <summary>
  ///   Initializes a new instance of the <see cref="MinioConnectionBuilder" /> class with a connection string.
  /// </summary>
  /// <param name="connection_string">The connection string to parse.</param>
  public MinioConnectionBuilder(string connection_string) {
    if (string.IsNullOrEmpty(connection_string)) {
      throw new ArgumentNullException(nameof(connection_string));
    }

    Parse(connection_string);
  }

  /// <summary>
  ///   Gets or sets the access key.
  /// </summary>
  public string access_key { get; set; }

  /// <summary>
  ///   Gets or sets the secret key.
  /// </summary>
  public string secret_key { get; set; }

  /// <summary>
  ///   Gets or sets the region.
  /// </summary>
  public string region { get; set; }

  /// <summary>
  ///   Gets or sets the end point.
  /// </summary>
  public string end_point { get; set; }

  /// <summary>
  ///   Gets or sets the bucket name.
  /// </summary>
  public string bucket { get; set; }

  /// <summary>
  ///   Parses the connection string.
  /// </summary>
  /// <param name="connection_string">The connection string to parse.</param>
  private void Parse(string connection_string) {
    foreach (string[] option in connection_string
                                .Split(
                                  new[] {
                                    ';'
                                  },
                                  StringSplitOptions.RemoveEmptyEntries
                                )
                                .Where(kvp => kvp.Contains('='))
                                .Select(
                                  kvp => kvp.Split(
                                    new[] {
                                      '='
                                    },
                                    2
                                  )
                                )) {
      string option_key = option[0].Trim();
      string option_value = option[1].Trim();
      if (!ParseItem(option_key, option_value)) {
        throw new ArgumentException(
          $"The option '{option_key}' cannot be recognized in connection string.",
          nameof(connection_string)
        );
      }
    }
  }

  /// <summary>
  ///   Parses an individual connection string item.
  /// </summary>
  /// <param name="key">The option key.</param>
  /// <param name="value">The option value.</param>
  /// <returns>True if the item was successfully parsed; otherwise, false.</returns>
  protected virtual bool ParseItem(string key, string value) {
    if (string.Equals(key, "AccessKey", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "Access Key", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "AccessKeyId", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
          key,
          "Access Key Id",
          StringComparison.OrdinalIgnoreCase
        ) ||
        string.Equals(key, "Id", StringComparison.OrdinalIgnoreCase)) {
      access_key = value;
      return true;
    }

    if (string.Equals(key, "SecretKey", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "Secret Key", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
          key,
          "SecretAccessKey",
          StringComparison.OrdinalIgnoreCase
        ) ||
        string.Equals(
          key,
          "Secret Access Key",
          StringComparison.OrdinalIgnoreCase
        ) ||
        string.Equals(key, "Secret", StringComparison.OrdinalIgnoreCase)) {
      secret_key = value;
      return true;
    }

    if (string.Equals(key, "Region", StringComparison.OrdinalIgnoreCase)) {
      region = value;
      return true;
    }

    if (string.Equals(key, "EndPoint", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "End Point", StringComparison.OrdinalIgnoreCase)) {
      end_point = value;
      return true;
    }

    if (string.Equals(key, "Bucket", StringComparison.OrdinalIgnoreCase)) {
      bucket = value;
      return true;
    }

    return false;
  }

  /// <summary>
  ///   Returns the connection string representation of the builder.
  /// </summary>
  /// <returns>The connection string.</returns>
  public override string ToString() {
    string connection_string = string.Empty;
    if (!string.IsNullOrEmpty(access_key)) {
      connection_string += "AccessKey=" + access_key + ";";
    }

    if (!string.IsNullOrEmpty(secret_key)) {
      connection_string += "SecretKey=" + secret_key + ";";
    }

    if (!string.IsNullOrEmpty(region)) {
      connection_string += "Region=" + region + ";";
    }

    if (!string.IsNullOrEmpty(end_point)) {
      connection_string += "EndPoint=" + end_point + ";";
    }

    if (!string.IsNullOrEmpty(bucket)) {
      connection_string += "Bucket=" + bucket + ";";
    }

    return connection_string;
  }
}
