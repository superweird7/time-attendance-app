using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Repository interface for Attendance Log operations.
    /// </summary>
    public interface IAttendanceRepository
    {
        /// <summary>
        /// Gets attendance logs for a date range.
        /// </summary>
        Task<List<AttendanceLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, List<int> departmentIds = null);

        /// <summary>
        /// Gets attendance logs for a specific user and date range.
        /// </summary>
        Task<List<AttendanceLog>> GetByUserAndDateRangeAsync(string badgeNumber, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Adds multiple attendance logs (from device sync).
        /// </summary>
        Task<int> AddBulkAsync(List<AttendanceLog> logs);

        /// <summary>
        /// Gets the latest attendance log for a user on a specific date.
        /// </summary>
        Task<AttendanceLog> GetLatestForUserOnDateAsync(string badgeNumber, DateTime date);

        /// <summary>
        /// Gets distinct badge numbers that have attendance on a specific date.
        /// </summary>
        Task<List<string>> GetPresentBadgesOnDateAsync(DateTime date);

        /// <summary>
        /// Gets attendance count by department for a date range.
        /// </summary>
        Task<Dictionary<int, int>> GetAttendanceCountByDepartmentAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Deletes attendance logs for a specific machine.
        /// </summary>
        Task DeleteByMachineAsync(int machineId);
    }
}
