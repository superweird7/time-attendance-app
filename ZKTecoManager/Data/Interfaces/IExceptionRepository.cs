using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZKTecoManager.Data.Interfaces
{
    /// <summary>
    /// Repository interface for Employee Exception (leave/absence) operations.
    /// </summary>
    public interface IExceptionRepository
    {
        /// <summary>
        /// Gets exceptions for a user on a specific date.
        /// </summary>
        Task<EmployeeException> GetByUserAndDateAsync(int userId, DateTime date);

        /// <summary>
        /// Gets all exceptions for a date range.
        /// </summary>
        Task<List<EmployeeException>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, List<int> departmentIds = null);

        /// <summary>
        /// Gets exceptions for a specific user within a date range.
        /// </summary>
        Task<List<EmployeeException>> GetByUserAndDateRangeAsync(int userId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Adds a new exception.
        /// </summary>
        Task<int> AddAsync(EmployeeException exception);

        /// <summary>
        /// Adds multiple exceptions (bulk operation).
        /// </summary>
        Task<int> AddBulkAsync(List<EmployeeException> exceptions);

        /// <summary>
        /// Updates an existing exception.
        /// </summary>
        Task UpdateAsync(EmployeeException exception);

        /// <summary>
        /// Deletes an exception.
        /// </summary>
        Task DeleteAsync(int id);

        /// <summary>
        /// Gets count of users on leave for a specific date.
        /// </summary>
        Task<int> GetOnLeaveCountAsync(DateTime date, List<int> departmentIds = null);

        /// <summary>
        /// Gets all exception types.
        /// </summary>
        Task<List<ExceptionType>> GetExceptionTypesAsync();
    }
}
