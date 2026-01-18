using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Leave;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository implementation for leave management data access
    /// </summary>
    public class LeaveRepository : BaseRepository, ILeaveRepository
    {
        #region Leave Types

        public async Task<List<LeaveType>> GetAllLeaveTypesAsync(bool activeOnly = true)
        {
            return await ExecuteAsync(async conn =>
            {
                var types = new List<LeaveType>();
                var sql = @"SELECT leave_type_id, leave_type_code, leave_type_name_ar, leave_type_name_en,
                           accrual_type, accrual_rate, accrual_cap_monthly, is_cumulative, annual_max,
                           reset_on_year_start, max_days_per_month, deducts_from_balance, is_active,
                           display_order, created_at, updated_at
                    FROM leave_types";

                if (activeOnly)
                    sql += " WHERE is_active = TRUE";

                sql += " ORDER BY display_order, leave_type_name_ar";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        types.Add(MapLeaveType(reader));
                    }
                }
                return types;
            });
        }

        public async Task<LeaveType> GetLeaveTypeByIdAsync(int leaveTypeId)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT leave_type_id, leave_type_code, leave_type_name_ar, leave_type_name_en,
                           accrual_type, accrual_rate, accrual_cap_monthly, is_cumulative, annual_max,
                           reset_on_year_start, max_days_per_month, deducts_from_balance, is_active,
                           display_order, created_at, updated_at
                    FROM leave_types WHERE leave_type_id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", leaveTypeId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return MapLeaveType(reader);
                    }
                }
                return null;
            });
        }

        public async Task<LeaveType> GetLeaveTypeByCodeAsync(string code)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT leave_type_id, leave_type_code, leave_type_name_ar, leave_type_name_en,
                           accrual_type, accrual_rate, accrual_cap_monthly, is_cumulative, annual_max,
                           reset_on_year_start, max_days_per_month, deducts_from_balance, is_active,
                           display_order, created_at, updated_at
                    FROM leave_types WHERE leave_type_code = @code";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("code", code);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return MapLeaveType(reader);
                    }
                }
                return null;
            });
        }

        private LeaveType MapLeaveType(NpgsqlDataReader reader)
        {
            return new LeaveType
            {
                LeaveTypeId = reader.GetInt32(0),
                LeaveTypeCode = reader.GetString(1),
                LeaveTypeNameAr = reader.GetString(2),
                LeaveTypeNameEn = reader.GetString(3),
                AccrualType = reader.GetString(4),
                AccrualRate = reader.GetDecimal(5),
                AccrualCapMonthly = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                IsCumulative = reader.GetBoolean(7),
                AnnualMax = reader.IsDBNull(8) ? (decimal?)null : reader.GetDecimal(8),
                ResetOnYearStart = reader.GetBoolean(9),
                MaxDaysPerMonth = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                DeductsFromBalance = reader.GetBoolean(11),
                IsActive = reader.GetBoolean(12),
                DisplayOrder = reader.GetInt32(13),
                CreatedAt = reader.GetDateTime(14),
                UpdatedAt = reader.GetDateTime(15)
            };
        }

        #endregion

        #region Leave Balances

        public async Task<List<LeaveBalance>> GetBalancesByUserAsync(int userId, int year)
        {
            return await ExecuteAsync(async conn =>
            {
                var balances = new List<LeaveBalance>();
                var sql = @"SELECT b.balance_id, b.user_id_fk, b.leave_type_id_fk, b.year,
                           b.total_accrued, b.used_days, b.carried_over, b.manual_adjustment,
                           b.last_accrual_date, b.created_at, b.updated_at,
                           u.name, u.badge_number, d.dept_name,
                           lt.leave_type_name_ar, lt.leave_type_name_en, lt.leave_type_code
                    FROM leave_balances b
                    JOIN users u ON b.user_id_fk = u.user_id
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    JOIN leave_types lt ON b.leave_type_id_fk = lt.leave_type_id
                    WHERE b.user_id_fk = @userId AND b.year = @year
                    ORDER BY lt.display_order";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    cmd.Parameters.AddWithValue("year", year);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            balances.Add(MapLeaveBalance(reader));
                        }
                    }
                }
                return balances;
            });
        }

        public async Task<List<LeaveBalance>> GetAllBalancesAsync(int year, int? departmentId = null, List<int> departmentIds = null)
        {
            return await ExecuteAsync(async conn =>
            {
                var balances = new List<LeaveBalance>();
                var sql = @"SELECT b.balance_id, b.user_id_fk, b.leave_type_id_fk, b.year,
                           b.total_accrued, b.used_days, b.carried_over, b.manual_adjustment,
                           b.last_accrual_date, b.created_at, b.updated_at,
                           u.name, u.badge_number, d.dept_name,
                           lt.leave_type_name_ar, lt.leave_type_name_en, lt.leave_type_code
                    FROM leave_balances b
                    JOIN users u ON b.user_id_fk = u.user_id
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    JOIN leave_types lt ON b.leave_type_id_fk = lt.leave_type_id
                    WHERE b.year = @year";

                if (departmentId.HasValue)
                    sql += " AND u.default_dept_id = @deptId";
                else if (departmentIds != null && departmentIds.Count > 0)
                    sql += " AND u.default_dept_id = ANY(@deptIds)";

                sql += " ORDER BY u.name, lt.display_order";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("year", year);
                    if (departmentId.HasValue)
                        cmd.Parameters.AddWithValue("deptId", departmentId.Value);
                    else if (departmentIds != null && departmentIds.Count > 0)
                        cmd.Parameters.AddWithValue("deptIds", departmentIds.ToArray());

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            balances.Add(MapLeaveBalance(reader));
                        }
                    }
                }
                return balances;
            });
        }

        public async Task<LeaveBalance> GetBalanceAsync(int userId, int leaveTypeId, int year)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT b.balance_id, b.user_id_fk, b.leave_type_id_fk, b.year,
                           b.total_accrued, b.used_days, b.carried_over, b.manual_adjustment,
                           b.last_accrual_date, b.created_at, b.updated_at,
                           u.name, u.badge_number, d.dept_name,
                           lt.leave_type_name_ar, lt.leave_type_name_en, lt.leave_type_code
                    FROM leave_balances b
                    JOIN users u ON b.user_id_fk = u.user_id
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    JOIN leave_types lt ON b.leave_type_id_fk = lt.leave_type_id
                    WHERE b.user_id_fk = @userId AND b.leave_type_id_fk = @leaveTypeId AND b.year = @year";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    cmd.Parameters.AddWithValue("leaveTypeId", leaveTypeId);
                    cmd.Parameters.AddWithValue("year", year);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return MapLeaveBalance(reader);
                    }
                }
                return null;
            });
        }

        public async Task<int> CreateBalanceAsync(LeaveBalance balance)
        {
            var sql = @"INSERT INTO leave_balances (user_id_fk, leave_type_id_fk, year, total_accrued,
                       used_days, carried_over, manual_adjustment, last_accrual_date)
                VALUES (@userId, @leaveTypeId, @year, @totalAccrued, @usedDays, @carriedOver,
                       @manualAdjustment, @lastAccrualDate)
                ON CONFLICT (user_id_fk, leave_type_id_fk, year) DO UPDATE SET
                    total_accrued = EXCLUDED.total_accrued,
                    used_days = EXCLUDED.used_days,
                    carried_over = EXCLUDED.carried_over,
                    manual_adjustment = EXCLUDED.manual_adjustment,
                    last_accrual_date = EXCLUDED.last_accrual_date,
                    updated_at = CURRENT_TIMESTAMP
                RETURNING balance_id";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("userId", balance.UserId),
                new NpgsqlParameter("leaveTypeId", balance.LeaveTypeId),
                new NpgsqlParameter("year", balance.Year),
                new NpgsqlParameter("totalAccrued", balance.TotalAccrued),
                new NpgsqlParameter("usedDays", balance.UsedDays),
                new NpgsqlParameter("carriedOver", balance.CarriedOver),
                new NpgsqlParameter("manualAdjustment", balance.ManualAdjustment),
                new NpgsqlParameter("lastAccrualDate", (object)balance.LastAccrualDate ?? DBNull.Value));

            return Convert.ToInt32(result);
        }

        public async Task UpdateBalanceAsync(LeaveBalance balance)
        {
            var sql = @"UPDATE leave_balances SET
                       total_accrued = @totalAccrued,
                       used_days = @usedDays,
                       carried_over = @carriedOver,
                       manual_adjustment = @manualAdjustment,
                       last_accrual_date = @lastAccrualDate,
                       updated_at = CURRENT_TIMESTAMP
                WHERE balance_id = @balanceId";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("balanceId", balance.BalanceId),
                new NpgsqlParameter("totalAccrued", balance.TotalAccrued),
                new NpgsqlParameter("usedDays", balance.UsedDays),
                new NpgsqlParameter("carriedOver", balance.CarriedOver),
                new NpgsqlParameter("manualAdjustment", balance.ManualAdjustment),
                new NpgsqlParameter("lastAccrualDate", (object)balance.LastAccrualDate ?? DBNull.Value));
        }

        public async Task InitializeBalancesForUserAsync(int userId, int year)
        {
            await ExecuteAsync(async conn =>
            {
                var sql = @"INSERT INTO leave_balances (user_id_fk, leave_type_id_fk, year, total_accrued, used_days, carried_over, manual_adjustment)
                    SELECT @userId, leave_type_id, @year, 0, 0, 0, 0
                    FROM leave_types
                    WHERE is_active = TRUE AND deducts_from_balance = TRUE
                    ON CONFLICT (user_id_fk, leave_type_id_fk, year) DO NOTHING";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    cmd.Parameters.AddWithValue("year", year);
                    await cmd.ExecuteNonQueryAsync();
                }
            });
        }

        public async Task InitializeBalancesForAllUsersAsync(int year)
        {
            await ExecuteAsync(async conn =>
            {
                var sql = @"INSERT INTO leave_balances (user_id_fk, leave_type_id_fk, year, total_accrued, used_days, carried_over, manual_adjustment)
                    SELECT u.user_id, lt.leave_type_id, @year, 0, 0, 0, 0
                    FROM users u
                    CROSS JOIN leave_types lt
                    WHERE u.is_active = TRUE AND lt.is_active = TRUE AND lt.deducts_from_balance = TRUE
                    ON CONFLICT (user_id_fk, leave_type_id_fk, year) DO NOTHING";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("year", year);
                    await cmd.ExecuteNonQueryAsync();
                }
            });
        }

        private LeaveBalance MapLeaveBalance(NpgsqlDataReader reader)
        {
            return new LeaveBalance
            {
                BalanceId = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                LeaveTypeId = reader.GetInt32(2),
                Year = reader.GetInt32(3),
                TotalAccrued = reader.GetDecimal(4),
                UsedDays = reader.GetDecimal(5),
                CarriedOver = reader.GetDecimal(6),
                ManualAdjustment = reader.GetDecimal(7),
                LastAccrualDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10),
                EmployeeName = reader.GetString(11),
                BadgeNumber = reader.GetString(12),
                DepartmentName = reader.IsDBNull(13) ? null : reader.GetString(13),
                LeaveTypeNameAr = reader.GetString(14),
                LeaveTypeNameEn = reader.GetString(15),
                LeaveTypeCode = reader.GetString(16),
                LeaveTypeName = $"{reader.GetString(14)} - {reader.GetString(15)}"
            };
        }

        #endregion

        #region Leave Transactions

        public async Task<List<LeaveTransaction>> GetTransactionsByUserAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            return await ExecuteAsync(async conn =>
            {
                var transactions = new List<LeaveTransaction>();
                var sql = @"SELECT t.transaction_id, t.user_id_fk, t.leave_type_id_fk, t.balance_id_fk,
                           t.transaction_type, t.days_amount, t.hours_amount, t.start_date, t.end_date,
                           t.submission_date, t.reason, t.notes, t.created_by, t.created_at,
                           u.name, u.badge_number, lt.leave_type_name_ar, lt.leave_type_name_en,
                           cb.name as created_by_name
                    FROM leave_transactions t
                    JOIN users u ON t.user_id_fk = u.user_id
                    JOIN leave_types lt ON t.leave_type_id_fk = lt.leave_type_id
                    LEFT JOIN users cb ON t.created_by = cb.user_id
                    WHERE t.user_id_fk = @userId";

                if (startDate.HasValue)
                    sql += " AND t.submission_date >= @startDate";
                if (endDate.HasValue)
                    sql += " AND t.submission_date <= @endDate";

                sql += " ORDER BY t.created_at DESC";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    if (startDate.HasValue)
                        cmd.Parameters.AddWithValue("startDate", startDate.Value);
                    if (endDate.HasValue)
                        cmd.Parameters.AddWithValue("endDate", endDate.Value);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            transactions.Add(MapLeaveTransaction(reader));
                        }
                    }
                }
                return transactions;
            });
        }

        public async Task<List<LeaveTransaction>> GetAllTransactionsAsync(DateTime startDate, DateTime endDate, int? departmentId = null, List<int> departmentIds = null)
        {
            return await ExecuteAsync(async conn =>
            {
                var transactions = new List<LeaveTransaction>();
                var sql = @"SELECT t.transaction_id, t.user_id_fk, t.leave_type_id_fk, t.balance_id_fk,
                           t.transaction_type, t.days_amount, t.hours_amount, t.start_date, t.end_date,
                           t.submission_date, t.reason, t.notes, t.created_by, t.created_at,
                           u.name, u.badge_number, lt.leave_type_name_ar, lt.leave_type_name_en,
                           cb.name as created_by_name
                    FROM leave_transactions t
                    JOIN users u ON t.user_id_fk = u.user_id
                    JOIN leave_types lt ON t.leave_type_id_fk = lt.leave_type_id
                    LEFT JOIN users cb ON t.created_by = cb.user_id
                    WHERE t.submission_date >= @startDate AND t.submission_date <= @endDate";

                if (departmentId.HasValue)
                    sql += " AND u.default_dept_id = @deptId";
                else if (departmentIds != null && departmentIds.Count > 0)
                    sql += " AND u.default_dept_id = ANY(@deptIds)";

                sql += " ORDER BY t.created_at DESC";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("startDate", startDate);
                    cmd.Parameters.AddWithValue("endDate", endDate);
                    if (departmentId.HasValue)
                        cmd.Parameters.AddWithValue("deptId", departmentId.Value);
                    else if (departmentIds != null && departmentIds.Count > 0)
                        cmd.Parameters.AddWithValue("deptIds", departmentIds.ToArray());

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            transactions.Add(MapLeaveTransaction(reader));
                        }
                    }
                }
                return transactions;
            });
        }

        public async Task<int> AddTransactionAsync(LeaveTransaction transaction)
        {
            var sql = @"INSERT INTO leave_transactions (user_id_fk, leave_type_id_fk, balance_id_fk,
                       transaction_type, days_amount, hours_amount, start_date, end_date,
                       submission_date, reason, notes, created_by)
                VALUES (@userId, @leaveTypeId, @balanceId, @transactionType, @daysAmount, @hoursAmount,
                       @startDate, @endDate, @submissionDate, @reason, @notes, @createdBy)
                RETURNING transaction_id";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("userId", transaction.UserId),
                new NpgsqlParameter("leaveTypeId", transaction.LeaveTypeId),
                new NpgsqlParameter("balanceId", (object)transaction.BalanceId ?? DBNull.Value),
                new NpgsqlParameter("transactionType", transaction.TransactionType),
                new NpgsqlParameter("daysAmount", transaction.DaysAmount),
                new NpgsqlParameter("hoursAmount", (object)transaction.HoursAmount ?? DBNull.Value),
                new NpgsqlParameter("startDate", (object)transaction.StartDate ?? DBNull.Value),
                new NpgsqlParameter("endDate", (object)transaction.EndDate ?? DBNull.Value),
                new NpgsqlParameter("submissionDate", transaction.SubmissionDate),
                new NpgsqlParameter("reason", (object)transaction.Reason ?? DBNull.Value),
                new NpgsqlParameter("notes", (object)transaction.Notes ?? DBNull.Value),
                new NpgsqlParameter("createdBy", (object)transaction.CreatedBy ?? DBNull.Value));

            return Convert.ToInt32(result);
        }

        public async Task<decimal> GetUsedDaysInMonthAsync(int userId, int leaveTypeId, int year, int month)
        {
            var sql = @"SELECT COALESCE(SUM(ABS(days_amount)), 0)
                FROM leave_transactions
                WHERE user_id_fk = @userId
                  AND leave_type_id_fk = @leaveTypeId
                  AND transaction_type = 'deduction'
                  AND EXTRACT(YEAR FROM submission_date) = @year
                  AND EXTRACT(MONTH FROM submission_date) = @month";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("userId", userId),
                new NpgsqlParameter("leaveTypeId", leaveTypeId),
                new NpgsqlParameter("year", year),
                new NpgsqlParameter("month", month));

            return Convert.ToDecimal(result);
        }

        private LeaveTransaction MapLeaveTransaction(NpgsqlDataReader reader)
        {
            return new LeaveTransaction
            {
                TransactionId = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                LeaveTypeId = reader.GetInt32(2),
                BalanceId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                TransactionType = reader.GetString(4),
                DaysAmount = reader.GetDecimal(5),
                HoursAmount = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                StartDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                EndDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                SubmissionDate = reader.GetDateTime(9),
                Reason = reader.IsDBNull(10) ? null : reader.GetString(10),
                Notes = reader.IsDBNull(11) ? null : reader.GetString(11),
                CreatedBy = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12),
                CreatedAt = reader.GetDateTime(13),
                EmployeeName = reader.GetString(14),
                BadgeNumber = reader.GetString(15),
                LeaveTypeNameAr = reader.GetString(16),
                LeaveTypeNameEn = reader.GetString(17),
                CreatedByName = reader.IsDBNull(18) ? null : reader.GetString(18)
            };
        }

        #endregion

        #region Hourly Leave

        public async Task<HourlyLeaveAccumulator> GetHourlyAccumulatorAsync(int userId)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT h.accumulator_id, h.user_id_fk, h.accumulated_hours,
                           h.last_conversion_date, h.total_hours_converted, h.total_days_deducted,
                           h.created_at, h.updated_at, u.name, u.badge_number
                    FROM hourly_leave_accumulator h
                    JOIN users u ON h.user_id_fk = u.user_id
                    WHERE h.user_id_fk = @userId";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return MapHourlyAccumulator(reader);
                    }
                }
                return null;
            });
        }

        public async Task CreateOrUpdateHourlyAccumulatorAsync(HourlyLeaveAccumulator accumulator)
        {
            var sql = @"INSERT INTO hourly_leave_accumulator (user_id_fk, accumulated_hours,
                       last_conversion_date, total_hours_converted, total_days_deducted)
                VALUES (@userId, @accumulatedHours, @lastConversionDate, @totalHoursConverted, @totalDaysDeducted)
                ON CONFLICT (user_id_fk) DO UPDATE SET
                    accumulated_hours = EXCLUDED.accumulated_hours,
                    last_conversion_date = EXCLUDED.last_conversion_date,
                    total_hours_converted = EXCLUDED.total_hours_converted,
                    total_days_deducted = EXCLUDED.total_days_deducted,
                    updated_at = CURRENT_TIMESTAMP";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("userId", accumulator.UserId),
                new NpgsqlParameter("accumulatedHours", accumulator.AccumulatedHours),
                new NpgsqlParameter("lastConversionDate", (object)accumulator.LastConversionDate ?? DBNull.Value),
                new NpgsqlParameter("totalHoursConverted", accumulator.TotalHoursConverted),
                new NpgsqlParameter("totalDaysDeducted", accumulator.TotalDaysDeducted));
        }

        public async Task<List<HourlyLeaveAccumulator>> GetAllHourlyAccumulatorsAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var accumulators = new List<HourlyLeaveAccumulator>();
                var sql = @"SELECT h.accumulator_id, h.user_id_fk, h.accumulated_hours,
                           h.last_conversion_date, h.total_hours_converted, h.total_days_deducted,
                           h.created_at, h.updated_at, u.name, u.badge_number
                    FROM hourly_leave_accumulator h
                    JOIN users u ON h.user_id_fk = u.user_id
                    WHERE h.accumulated_hours > 0
                    ORDER BY u.name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        accumulators.Add(MapHourlyAccumulator(reader));
                    }
                }
                return accumulators;
            });
        }

        private HourlyLeaveAccumulator MapHourlyAccumulator(NpgsqlDataReader reader)
        {
            return new HourlyLeaveAccumulator
            {
                AccumulatorId = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                AccumulatedHours = reader.GetDecimal(2),
                LastConversionDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                TotalHoursConverted = reader.GetDecimal(4),
                TotalDaysDeducted = reader.GetInt32(5),
                CreatedAt = reader.GetDateTime(6),
                UpdatedAt = reader.GetDateTime(7),
                EmployeeName = reader.GetString(8),
                BadgeNumber = reader.GetString(9)
            };
        }

        #endregion

        #region Long-Term Leave

        public async Task<List<LongTermLeaveEntry>> GetActiveLongTermLeavesAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var entries = new List<LongTermLeaveEntry>();
                var sql = @"SELECT l.registry_id, l.user_id_fk, l.leave_type, l.start_date, l.end_date,
                           l.stop_accruals, l.notes, l.created_by, l.created_at, l.updated_at,
                           u.name, u.badge_number, cb.name as created_by_name
                    FROM long_term_leave_registry l
                    JOIN users u ON l.user_id_fk = u.user_id
                    LEFT JOIN users cb ON l.created_by = cb.user_id
                    WHERE l.end_date IS NULL OR l.end_date >= CURRENT_DATE
                    ORDER BY l.start_date DESC";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        entries.Add(MapLongTermLeave(reader));
                    }
                }
                return entries;
            });
        }

        public async Task<LongTermLeaveEntry> GetActiveLongTermLeaveByUserAsync(int userId)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT l.registry_id, l.user_id_fk, l.leave_type, l.start_date, l.end_date,
                           l.stop_accruals, l.notes, l.created_by, l.created_at, l.updated_at,
                           u.name, u.badge_number, cb.name as created_by_name
                    FROM long_term_leave_registry l
                    JOIN users u ON l.user_id_fk = u.user_id
                    LEFT JOIN users cb ON l.created_by = cb.user_id
                    WHERE l.user_id_fk = @userId AND (l.end_date IS NULL OR l.end_date >= CURRENT_DATE)
                    ORDER BY l.start_date DESC
                    LIMIT 1";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                            return MapLongTermLeave(reader);
                    }
                }
                return null;
            });
        }

        public async Task<int> AddLongTermLeaveAsync(LongTermLeaveEntry entry)
        {
            var sql = @"INSERT INTO long_term_leave_registry (user_id_fk, leave_type, start_date, end_date,
                       stop_accruals, notes, created_by)
                VALUES (@userId, @leaveType, @startDate, @endDate, @stopAccruals, @notes, @createdBy)
                RETURNING registry_id";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("userId", entry.UserId),
                new NpgsqlParameter("leaveType", entry.LeaveType),
                new NpgsqlParameter("startDate", entry.StartDate),
                new NpgsqlParameter("endDate", (object)entry.EndDate ?? DBNull.Value),
                new NpgsqlParameter("stopAccruals", entry.StopAccruals),
                new NpgsqlParameter("notes", (object)entry.Notes ?? DBNull.Value),
                new NpgsqlParameter("createdBy", (object)entry.CreatedBy ?? DBNull.Value));

            return Convert.ToInt32(result);
        }

        public async Task EndLongTermLeaveAsync(int registryId, DateTime endDate)
        {
            var sql = @"UPDATE long_term_leave_registry SET
                       end_date = @endDate,
                       updated_at = CURRENT_TIMESTAMP
                WHERE registry_id = @registryId";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("registryId", registryId),
                new NpgsqlParameter("endDate", endDate));
        }

        public async Task<List<int>> GetUsersOnLongTermLeaveAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var userIds = new List<int>();
                var sql = @"SELECT DISTINCT user_id_fk
                    FROM long_term_leave_registry
                    WHERE stop_accruals = TRUE
                      AND start_date <= CURRENT_DATE
                      AND (end_date IS NULL OR end_date >= CURRENT_DATE)";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        userIds.Add(reader.GetInt32(0));
                    }
                }
                return userIds;
            });
        }

        private LongTermLeaveEntry MapLongTermLeave(NpgsqlDataReader reader)
        {
            return new LongTermLeaveEntry
            {
                RegistryId = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                LeaveType = reader.GetString(2),
                StartDate = reader.GetDateTime(3),
                EndDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                StopAccruals = reader.GetBoolean(5),
                Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedBy = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                CreatedAt = reader.GetDateTime(8),
                UpdatedAt = reader.GetDateTime(9),
                EmployeeName = reader.GetString(10),
                BadgeNumber = reader.GetString(11),
                CreatedByName = reader.IsDBNull(12) ? null : reader.GetString(12)
            };
        }

        #endregion

        #region Accrual Settings

        public async Task<LeaveAccrualSettings> GetAccrualSettingsAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT setting_id, accrual_enabled, accrual_check_time, last_accrual_run,
                           hours_per_day, created_at, updated_at
                    FROM leave_accrual_settings
                    WHERE setting_id = 1";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new LeaveAccrualSettings
                        {
                            SettingId = reader.GetInt32(0),
                            AccrualEnabled = reader.GetBoolean(1),
                            AccrualCheckTime = reader.GetTimeSpan(2),
                            LastAccrualRun = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            HoursPerDay = reader.GetInt32(4),
                            CreatedAt = reader.GetDateTime(5),
                            UpdatedAt = reader.GetDateTime(6)
                        };
                    }
                }
                return LeaveAccrualSettings.Default;
            });
        }

        public async Task UpdateAccrualSettingsAsync(LeaveAccrualSettings settings)
        {
            var sql = @"UPDATE leave_accrual_settings SET
                       accrual_enabled = @enabled,
                       accrual_check_time = @checkTime,
                       hours_per_day = @hoursPerDay,
                       updated_at = CURRENT_TIMESTAMP
                WHERE setting_id = 1";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("enabled", settings.AccrualEnabled),
                new NpgsqlParameter("checkTime", settings.AccrualCheckTime),
                new NpgsqlParameter("hoursPerDay", settings.HoursPerDay));
        }

        public async Task UpdateLastAccrualRunAsync(DateTime timestamp)
        {
            var sql = @"UPDATE leave_accrual_settings SET
                       last_accrual_run = @timestamp,
                       updated_at = CURRENT_TIMESTAMP
                WHERE setting_id = 1";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("timestamp", timestamp));
        }

        #endregion

        #region Accrual Processing

        public async Task<List<int>> GetUsersEligibleForAccrualAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var userIds = new List<int>();
                // Get active users who are NOT on long-term leave
                var sql = @"SELECT u.user_id
                    FROM users u
                    WHERE u.is_active = TRUE
                      AND u.user_id NOT IN (
                          SELECT DISTINCT user_id_fk
                          FROM long_term_leave_registry
                          WHERE stop_accruals = TRUE
                            AND start_date <= CURRENT_DATE
                            AND (end_date IS NULL OR end_date >= CURRENT_DATE)
                      )
                    ORDER BY u.user_id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        userIds.Add(reader.GetInt32(0));
                    }
                }
                return userIds;
            });
        }

        public async Task UpdateLastAccrualDateAsync(int balanceId, DateTime date)
        {
            var sql = @"UPDATE leave_balances SET
                       last_accrual_date = @date,
                       updated_at = CURRENT_TIMESTAMP
                WHERE balance_id = @balanceId";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("balanceId", balanceId),
                new NpgsqlParameter("date", date));
        }

        #endregion
    }
}
