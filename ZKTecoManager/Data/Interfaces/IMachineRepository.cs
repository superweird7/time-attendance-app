using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Repository interface for Machine (Device) operations.
    /// </summary>
    public interface IMachineRepository : IRepository<Machine>
    {
        /// <summary>
        /// Gets a machine by its IP address.
        /// </summary>
        Task<Machine> GetByIpAsync(string ipAddress);

        /// <summary>
        /// Gets machines accessible by the current user based on role and permissions.
        /// </summary>
        Task<List<Machine>> GetAccessibleMachinesAsync(int? userId, string userRole);

        /// <summary>
        /// Updates the last sync time for a machine.
        /// </summary>
        Task UpdateLastSyncAsync(int machineId, System.DateTime lastSync);

        /// <summary>
        /// Gets machines by department permission.
        /// </summary>
        Task<List<Machine>> GetByDepartmentPermissionAsync(int departmentId);

        /// <summary>
        /// Checks if a machine IP already exists.
        /// </summary>
        Task<bool> IpExistsAsync(string ipAddress, int? excludeId = null);
    }
}
