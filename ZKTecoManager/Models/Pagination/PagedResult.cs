using System;
using System.Collections.Generic;

namespace ZKTecoManager.Models.Pagination
{
    /// <summary>
    /// Represents a paginated result set.
    /// </summary>
    /// <typeparam name="T">The type of items in the result set.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// The items for the current page.
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Total number of items across all pages.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number (1-based).
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Number of items per page.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages.
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

        /// <summary>
        /// Whether there is a previous page.
        /// </summary>
        public bool HasPrevious => Page > 1;

        /// <summary>
        /// Whether there is a next page.
        /// </summary>
        public bool HasNext => Page < TotalPages;

        /// <summary>
        /// Creates an empty paged result.
        /// </summary>
        public PagedResult() { }

        /// <summary>
        /// Creates a paged result with the specified values.
        /// </summary>
        public PagedResult(List<T> items, int totalCount, int page, int pageSize)
        {
            Items = items ?? new List<T>();
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }
    }
}
