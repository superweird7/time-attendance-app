using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Models.Dashboard;

namespace ZKTecoManager.Services.Interfaces
{
    /// <summary>
    /// Service interface for Dashboard operations.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Gets the attendance KPIs for a specific date.
        /// </summary>
        Task<AttendanceKpiData> GetKpiDataAsync(DateTime date, List<int> departmentIds = null);

        /// <summary>
        /// Gets the list of absent employees for a specific date.
        /// </summary>
        Task<List<AbsenteeInfo>> GetAbsenteesAsync(DateTime date, List<int> departmentIds = null);

        /// <summary>
        /// Gets the list of late arrivals for a specific date.
        /// </summary>
        Task<List<LateArrivalInfo>> GetLateArrivalsAsync(DateTime date, List<int> departmentIds = null);
    }
}
