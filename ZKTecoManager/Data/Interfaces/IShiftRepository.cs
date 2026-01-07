using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Repository interface for Shift operations.
    /// </summary>
    public interface IShiftRepository : IRepository<Shift>
    {
        /// <summary>
        /// Gets a shift by its name.
        /// </summary>
        Task<Shift> GetByNameAsync(string name);

        /// <summary>
        /// Checks if a shift name already exists.
        /// </summary>
        Task<bool> ExistsAsync(string name, int? excludeId = null);

        /// <summary>
        /// Gets all active shifts.
        /// </summary>
        Task<List<Shift>> GetActiveShiftsAsync();
    }
}
