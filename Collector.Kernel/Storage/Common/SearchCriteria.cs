using System.Text.RegularExpressions;

namespace Collector.Kernel.Storage.Common;

/// <summary>
///   Represents criteria for searching files, combining a prefix and a regular expression pattern.
/// </summary>
public class SearchCriteria {
  /// <summary>
  ///   Gets or sets the path prefix for the search.
  /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
  public string prefix { get; set; }

  /// <summary>
  ///   Gets or sets the regular expression pattern for matching file names.
  /// </summary>
  public Regex pattern { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}