#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Collector.Kernel.Storage.Common;

/// <summary>
///   Represents metadata for a file in storage.
/// </summary>
public class FileSpec {
  /// <summary>
  ///   Gets or sets the path of the file.
  /// </summary>
  public string path { get; set; }

  /// <summary>
  ///   Gets or sets the creation time of the file.
  /// </summary>
  public DateTime created { get; set; }

  /// <summary>
  ///   Gets or sets the last modification time of the file.
  /// </summary>
  public DateTime modified { get; set; }

  /// <summary>
  ///   Gets or sets the size of the file in bytes.
  /// </summary>
  public long size { get; set; }
}