namespace Collector.Kernel.Storage.Minio;

/// <summary>
///   Options for Minio storage.
/// </summary>
public class MinioStorageOptions {
  /// <summary>
  ///   Gets or sets the connection string.
  /// </summary>
  public required string connection_string { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether to automatically create the bucket.
  /// </summary>
  public bool auto_create_bucket { get; set; }
}