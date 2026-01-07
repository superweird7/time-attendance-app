namespace ZKTecoManager.Models.Pagination
{
    /// <summary>
    /// Parameters for pagination requests.
    /// </summary>
    public class PaginationParams
    {
        private int _page = 1;
        private int _pageSize = 50;

        /// <summary>
        /// Current page number (1-based). Defaults to 1.
        /// </summary>
        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Number of items per page. Defaults to 50. Maximum is 200.
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 1 : (value > 200 ? 200 : value);
        }

        /// <summary>
        /// Optional search term for filtering.
        /// </summary>
        public string SearchTerm { get; set; }

        /// <summary>
        /// Optional department ID for filtering.
        /// </summary>
        public int? DepartmentId { get; set; }

        /// <summary>
        /// Calculates the number of items to skip for SQL OFFSET.
        /// </summary>
        public int Skip => (Page - 1) * PageSize;

        /// <summary>
        /// Creates default pagination parameters.
        /// </summary>
        public PaginationParams() { }

        /// <summary>
        /// Creates pagination parameters with specified page and size.
        /// </summary>
        public PaginationParams(int page, int pageSize)
        {
            Page = page;
            PageSize = pageSize;
        }
    }
}
