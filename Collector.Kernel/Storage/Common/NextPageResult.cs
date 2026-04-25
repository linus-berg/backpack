#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Collector.Kernel.Storage.Common;

/// <summary>
///   Represents the result of a request for the next page of files.
/// </summary>
public class NextPageResult {
  /// <summary>
  ///   Gets or sets a value indicating whether the request was successful.
  /// </summary>
  public bool success { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether there are more pages available.
  /// </summary>
  public bool has_more { get; set; }

  /// <summary>
  ///   Gets or sets the collection of files in this page.
  /// </summary>
  public IReadOnlyCollection<FileSpec> files { get; set; }

  /// <summary>
  ///   Gets or sets the function to call for retrieving the subsequent page.
  /// </summary>
  public Func<PagedFileListResult, Task<NextPageResult>> next_page_func {
    get;
    set;
  }
}