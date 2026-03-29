// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Collector.Kernel.Storage.Common;

/// <summary>
///   Defines an interface for objects that support retrieving the next page of results.
/// </summary>
public interface IHasNextPageFunc {
  /// <summary>
  ///   Gets or sets the function to retrieve the next page of results.
  /// </summary>
  Func<PagedFileListResult, Task<NextPageResult>> next_page_func { get; set; }
}

/// <summary>
///   Represents a paged result of file specifications.
/// </summary>
public class PagedFileListResult : IHasNextPageFunc {
  private static readonly IReadOnlyCollection<FileSpec> S_EMPTY_ =
    new ReadOnlyCollection<FileSpec>(Array.Empty<FileSpec>());

  /// <summary>
  ///   An empty paged result.
  /// </summary>
  public static readonly PagedFileListResult S_EMPTY = new(S_EMPTY_);

  /// <summary>
  ///   Initializes a new instance of the <see cref="PagedFileListResult" /> class with a fixed set of files.
  /// </summary>
  /// <param name="files">The collection of files.</param>
  public PagedFileListResult(IReadOnlyCollection<FileSpec> files) {
    this.files = files;
    has_more = false;
    ((IHasNextPageFunc)this).next_page_func = null;
  }

  /// <summary>
  ///   Initializes a new instance of the <see cref="PagedFileListResult" /> class with files and pagination support.
  /// </summary>
  /// <param name="files">The collection of files.</param>
  /// <param name="has_more">Whether there are more pages.</param>
  /// <param name="next_page_func">The function to retrieve the next page.</param>
  public PagedFileListResult(IReadOnlyCollection<FileSpec> files, bool has_more,
                             Func<PagedFileListResult, Task<NextPageResult>>
                               next_page_func) {
    this.files = files;
    this.has_more = has_more;
    ((IHasNextPageFunc)this).next_page_func = next_page_func;
  }

  /// <summary>
  ///   Initializes a new instance of the <see cref="PagedFileListResult" /> class with only a pagination function.
  /// </summary>
  /// <param name="next_page_func">The function to retrieve the next page.</param>
  public PagedFileListResult(
    Func<PagedFileListResult, Task<NextPageResult>> next_page_func) {
    ((IHasNextPageFunc)this).next_page_func = next_page_func;
  }

  /// <summary>
  ///   Gets the collection of files in the current page.
  /// </summary>
  public IReadOnlyCollection<FileSpec> files { get; private set; }

  /// <summary>
  ///   Gets a value indicating whether there are more pages available.
  /// </summary>
  public bool has_more { get; private set; }

  /// <summary>
  ///   Gets custom data associated with the paged result.
  /// </summary>
  protected IDictionary<string, object> data { get; } =
    new Dictionary<string, object>();

  Func<PagedFileListResult, Task<NextPageResult>> IHasNextPageFunc.
    next_page_func { get; set; }

  /// <summary>
  ///   Asynchronously loads the next page of results.
  /// </summary>
  /// <returns>True if the next page was successfully loaded; otherwise, false.</returns>
  public async Task<bool> NextPageAsync() {
    if (((IHasNextPageFunc)this).next_page_func == null) {
      return false;
    }

    NextPageResult result = await ((IHasNextPageFunc)this).next_page_func(this);
    if (result.success) {
      files = result.files;
      has_more = result.has_more;
      ((IHasNextPageFunc)this).next_page_func = result.next_page_func;
    } else {
      files = S_EMPTY_;
      has_more = false;
      ((IHasNextPageFunc)this).next_page_func = null;
    }

    return result.success;
  }
}