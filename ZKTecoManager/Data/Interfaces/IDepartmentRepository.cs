using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Repository interface for Department operations.
    /// </summary>
    public interface IDepartmentRepository : IRepository<Department>
    {
        /// <summary>
        /// Gets a department by its name.
        /// </summary>
        Task<Department> GetByNameAsync(string name);

        /// <summary>
        /// Gets departments accessible by the current user based on role.
        /// </summary>
        Task<List<Department>> GetAccessibleDepartmentsAsync(int? userId, string userRole);

        /// <summary>
        /// Gets departments by their IDs.
        /// </summary>
        Task<List<Department>> GetAccessibleDepartmentsAsync(List<int> departmentIds);

        /// <summary>
        /// Checks if a department name already exists.
        /// </summary>
        Task<bool> ExistsAsync(string name, int? excludeId = null);
    }
}
