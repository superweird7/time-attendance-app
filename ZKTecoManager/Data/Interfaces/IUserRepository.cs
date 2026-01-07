using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Models.Pagination;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Repository interface for User (Employee) operations.
    /// </summary>
    public interface IUserRepository : IRepository<User>
    {
        /// <summary>
        /// Gets users with pagination support.
        /// </summary>
        Task<PagedResult<User>> GetPagedAsync(PaginationParams pagination);

        /// <summary>
        /// Bulk assigns a shift to multiple users.
        /// </summary>
        Task<int> BulkAssignShiftAsync(List<int> userIds, int shiftId);

        /// <summary>
        /// Deletes multiple users by their IDs.
        /// </summary>
        Task DeleteMultipleAsync(List<int> userIds);

        /// <summary>
        /// Gets a user by badge number.
        /// </summary>
        Task<User> GetByBadgeNumberAsync(string badgeNumber);

        /// <summary>
        /// Gets all users in a specific department.
        /// </summary>
        Task<List<User>> GetByDepartmentAsync(int departmentId);

        /// <summary>
        /// Gets users accessible by the current user based on role and permissions.
        /// </summary>
        Task<List<User>> GetAccessibleUsersAsync(int? currentUserId, string userRole, List<int> departmentIds = null);

        /// <summary>
        /// Validates user credentials and returns the user if valid.
        /// </summary>
        Task<User> ValidateCredentialsAsync(string username, string password);

        /// <summary>
        /// Gets all users with their assigned shifts.
        /// </summary>
        Task<List<User>> GetUsersWithShiftsAsync();

        /// <summary>
        /// Checks if a badge number already exists.
        /// </summary>
        Task<bool> BadgeExistsAsync(string badgeNumber, int? excludeUserId = null);

        /// <summary>
        /// Gets the total count of users with optional filtering.
        /// </summary>
        Task<int> GetCountAsync(int? departmentId = null);
    }
}
