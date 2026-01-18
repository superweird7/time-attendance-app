using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Models.Leave;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Repository interface for leave management data access
    /// </summary>
    public interface ILeaveRepository
    {
        #region Leave Types

        /// <summary>
        /// Gets all leave types
        /// </summary>
        Task<List<LeaveType>> GetAllLeaveTypesAsync(bool activeOnly = true);

        /// <summary>
        /// Gets a leave type by its ID
        /// </summary>
        Task<LeaveType> GetLeaveTypeByIdAsync(int leaveTypeId);

        /// <summary>
        /// Gets a leave type by its code (e.g., "ORDINARY", "SICK_FULL")
        /// </summary>
        Task<LeaveType> GetLeaveTypeByCodeAsync(string code);

        #endregion

        #region Leave Balances

        /// <summary>
        /// Gets all balances for a specific user and year
        /// </summary>
        Task<List<LeaveBalance>> GetBalancesByUserAsync(int userId, int year);

        /// <summary>
        /// Gets all balances for a specific year, optionally filtered by department
        /// </summary>
        Task<List<LeaveBalance>> GetAllBalancesAsync(int year, int? departmentId = null, List<int> departmentIds = null);

        /// <summary>
        /// Gets a specific balance for user, leave type, and year
        /// </summary>
        Task<LeaveBalance> GetBalanceAsync(int userId, int leaveTypeId, int year);

        /// <summary>
        /// Creates a new balance record
        /// </summary>
        Task<int> CreateBalanceAsync(LeaveBalance balance);

        /// <summary>
        /// Updates an existing balance record
        /// </summary>
        Task UpdateBalanceAsync(LeaveBalance balance);

        /// <summary>
        /// Initializes balances for a user for all active leave types for a year
        /// </summary>
        Task InitializeBalancesForUserAsync(int userId, int year);

        /// <summary>
        /// Initializes balances for all users for a year
        /// </summary>
        Task InitializeBalancesForAllUsersAsync(int year);

        #endregion

        #region Leave Transactions

        /// <summary>
        /// Gets transactions for a user within optional date range
        /// </summary>
        Task<List<LeaveTransaction>> GetTransactionsByUserAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Gets all transactions within a date range, optionally filtered by department
        /// </summary>
        Task<List<LeaveTransaction>> GetAllTransactionsAsync(DateTime startDate, DateTime endDate, int? departmentId = null, List<int> departmentIds = null);

        /// <summary>
        /// Adds a new transaction
        /// </summary>
        Task<int> AddTransactionAsync(LeaveTransaction transaction);

        /// <summary>
        /// Gets total used days for a leave type in a specific month
        /// </summary>
        Task<decimal> GetUsedDaysInMonthAsync(int userId, int leaveTypeId, int year, int month);

        #endregion

        #region Hourly Leave

        /// <summary>
        /// Gets the hourly leave accumulator for a user
        /// </summary>
        Task<HourlyLeaveAccumulator> GetHourlyAccumulatorAsync(int userId);

        /// <summary>
        /// Creates or updates the hourly leave accumulator for a user
        /// </summary>
        Task CreateOrUpdateHourlyAccumulatorAsync(HourlyLeaveAccumulator accumulator);

        /// <summary>
        /// Gets all hourly leave accumulators
        /// </summary>
        Task<List<HourlyLeaveAccumulator>> GetAllHourlyAccumulatorsAsync();

        #endregion

        #region Long-Term Leave

        /// <summary>
        /// Gets all active long-term leave entries
        /// </summary>
        Task<List<LongTermLeaveEntry>> GetActiveLongTermLeavesAsync();

        /// <summary>
        /// Gets active long-term leave for a specific user
        /// </summary>
        Task<LongTermLeaveEntry> GetActiveLongTermLeaveByUserAsync(int userId);

        /// <summary>
        /// Adds a new long-term leave entry
        /// </summary>
        Task<int> AddLongTermLeaveAsync(LongTermLeaveEntry entry);

        /// <summary>
        /// Ends a long-term leave (sets end date)
        /// </summary>
        Task EndLongTermLeaveAsync(int registryId, DateTime endDate);

        /// <summary>
        /// Gets list of user IDs currently on long-term leave
        /// </summary>
        Task<List<int>> GetUsersOnLongTermLeaveAsync();

        #endregion

        #region Accrual Settings

        /// <summary>
        /// Gets the accrual settings
        /// </summary>
        Task<LeaveAccrualSettings> GetAccrualSettingsAsync();

        /// <summary>
        /// Updates the accrual settings
        /// </summary>
        Task UpdateAccrualSettingsAsync(LeaveAccrualSettings settings);

        /// <summary>
        /// Updates the last accrual run timestamp
        /// </summary>
        Task UpdateLastAccrualRunAsync(DateTime timestamp);

        #endregion

        #region Accrual Processing

        /// <summary>
        /// Gets list of user IDs eligible for accrual (active, not on long-term leave)
        /// </summary>
        Task<List<int>> GetUsersEligibleForAccrualAsync();

        /// <summary>
        /// Updates the last accrual date for a balance
        /// </summary>
        Task UpdateLastAccrualDateAsync(int balanceId, DateTime date);

        #endregion
    }
}
