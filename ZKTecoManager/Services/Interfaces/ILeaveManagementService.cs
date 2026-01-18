using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Models.Leave;

namespace ZKTecoManager.Services.Interfaces
{
    /// <summary>
    /// Service interface for leave management business logic
    /// </summary>
    public interface ILeaveManagementService
    {
        #region Balance Operations

        /// <summary>
        /// Gets all balances for an employee for a specific year
        /// </summary>
        Task<List<LeaveBalance>> GetEmployeeBalancesAsync(int userId, int year);

        /// <summary>
        /// Gets a specific balance for user, leave type, and year
        /// </summary>
        Task<LeaveBalance> GetBalanceAsync(int userId, int leaveTypeId, int year);

        /// <summary>
        /// Adjusts a balance manually (+/-)
        /// </summary>
        Task<(bool Success, string Message)> AdjustBalanceAsync(
            int userId, int leaveTypeId, int year, decimal adjustment, string reason, int createdBy);

        /// <summary>
        /// Initializes balances for a user for current year
        /// </summary>
        Task InitializeUserBalancesAsync(int userId);

        #endregion

        #region Leave Deduction

        /// <summary>
        /// Deducts leave from an employee's balance (direct admin deduction)
        /// </summary>
        Task<(bool Success, string Message)> DeductLeaveAsync(
            int userId, int leaveTypeId, DateTime startDate, DateTime endDate,
            string reason, int createdBy);

        /// <summary>
        /// Validates if a leave deduction is allowed
        /// </summary>
        Task<(bool IsValid, string Message)> ValidateLeaveDeductionAsync(
            int userId, int leaveTypeId, decimal days, DateTime startDate);

        #endregion

        #region Hourly Leave

        /// <summary>
        /// Adds hourly leave (direct hours input)
        /// </summary>
        Task<(bool Success, string Message)> AddHourlyLeaveAsync(
            int userId, decimal hours, DateTime submissionDate, string reason, int createdBy);

        /// <summary>
        /// Adds hourly leave using time range
        /// </summary>
        Task<(bool Success, string Message)> AddHourlyLeaveByTimeRangeAsync(
            int userId, TimeSpan startTime, TimeSpan endTime,
            DateTime submissionDate, string reason, int createdBy);

        /// <summary>
        /// Gets the hourly leave accumulator for a user
        /// </summary>
        Task<HourlyLeaveAccumulator> GetHourlyAccumulatorAsync(int userId);

        #endregion

        #region Validation

        /// <summary>
        /// Checks if unpaid leave is within the monthly limit (5 days/month)
        /// </summary>
        Task<bool> IsUnpaidLeaveWithinLimitAsync(int userId, int year, int month, decimal additionalDays);

        /// <summary>
        /// Checks if user is on long-term leave
        /// </summary>
        Task<bool> IsOnLongTermLeaveAsync(int userId);

        #endregion

        #region Long-Term Leave

        /// <summary>
        /// Starts a long-term leave for an employee
        /// </summary>
        Task<(bool Success, string Message)> StartLongTermLeaveAsync(
            int userId, string leaveType, DateTime startDate, string notes, int createdBy);

        /// <summary>
        /// Ends a long-term leave
        /// </summary>
        Task<(bool Success, string Message)> EndLongTermLeaveAsync(int registryId, DateTime endDate);

        /// <summary>
        /// Gets all active long-term leaves
        /// </summary>
        Task<List<LongTermLeaveEntry>> GetActiveLongTermLeavesAsync();

        #endregion

        #region Reports

        /// <summary>
        /// Gets leave history for an employee
        /// </summary>
        Task<List<LeaveTransaction>> GetLeaveHistoryAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Gets all transactions within a date range
        /// </summary>
        Task<List<LeaveTransaction>> GetAllTransactionsAsync(DateTime startDate, DateTime endDate, List<int> departmentIds = null);

        /// <summary>
        /// Gets all balances for a year filtered by departments
        /// </summary>
        Task<List<LeaveBalance>> GetDepartmentBalancesAsync(int year, List<int> departmentIds = null);

        #endregion

        #region Leave Types

        /// <summary>
        /// Gets all active leave types
        /// </summary>
        Task<List<LeaveType>> GetLeaveTypesAsync();

        /// <summary>
        /// Gets a leave type by code
        /// </summary>
        Task<LeaveType> GetLeaveTypeByCodeAsync(string code);

        #endregion
    }
}
