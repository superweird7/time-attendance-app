using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Leave;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// Background service that runs daily to process leave accruals.
    /// Handles: Ordinary leave (daily), Sick Full (monthly), Sick Half (monthly with annual cap)
    /// </summary>
    public static class DailyLeaveAccrualService
    {
        private static Timer _accrualTimer;
        private static volatile bool _isRunning = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Starts the daily accrual service
        /// </summary>
        public static void Start()
        {
            lock (_lockObject)
            {
                if (_isRunning) return;
                _isRunning = true;
            }

            // Check every hour, process once daily at configured time
            _accrualTimer = new Timer(CheckAndProcessAccruals, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

            System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Service started at {DateTime.Now}");
        }

        /// <summary>
        /// Stops the daily accrual service
        /// </summary>
        public static void Stop()
        {
            _accrualTimer?.Dispose();
            _accrualTimer = null;
            _isRunning = false;
            System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Service stopped at {DateTime.Now}");
        }

        /// <summary>
        /// Manually trigger accrual processing (for testing or manual runs)
        /// </summary>
        public static void RunAccrualsNow()
        {
            try
            {
                ProcessDailyAccruals();
                UpdateLastAccrualRun(DateTime.Now);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Manual run error: {ex.Message}");
            }
        }

        private static void CheckAndProcessAccruals(object state)
        {
            try
            {
                var settings = GetAccrualSettings();
                if (!settings.AccrualEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("[LeaveAccrual] Accrual is disabled");
                    return;
                }

                var now = DateTime.Now;
                var lastRun = settings.LastAccrualRun;

                // Run once per day at or after configured time
                bool shouldRun = false;
                if (lastRun == null || lastRun.Value.Date < now.Date)
                {
                    if (now.TimeOfDay >= settings.AccrualCheckTime)
                    {
                        shouldRun = true;
                    }
                }

                if (shouldRun)
                {
                    System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Starting daily accrual at {now}");
                    ProcessDailyAccruals();
                    UpdateLastAccrualRun(now);
                    System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Completed at {DateTime.Now}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Error: {ex.Message}");
            }
        }

        private static void ProcessDailyAccruals()
        {
            var today = DateTime.Today;
            var currentYear = today.Year;
            var currentMonth = today.Month;
            var isFirstDayOfMonth = today.Day == 1;
            var isFirstDayOfYear = today.Month == 1 && today.Day == 1;

            // Get users not on long-term leave
            var eligibleUserIds = GetEligibleUsers();
            System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Processing {eligibleUserIds.Count} eligible users");

            // Get leave types
            var leaveTypes = GetLeaveTypes();

            foreach (var userId in eligibleUserIds)
            {
                try
                {
                    foreach (var leaveType in leaveTypes)
                    {
                        ProcessUserAccrual(userId, leaveType, today, currentYear, currentMonth,
                            isFirstDayOfMonth, isFirstDayOfYear);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] Error for user {userId}: {ex.Message}");
                }
            }
        }

        private static void ProcessUserAccrual(int userId, LeaveType leaveType, DateTime today,
            int year, int month, bool isFirstDayOfMonth, bool isFirstDayOfYear)
        {
            if (leaveType.AccrualType == "none")
                return;

            // Handle year-start reset for non-cumulative types
            if (isFirstDayOfYear && leaveType.ResetOnYearStart)
            {
                ResetBalanceForYear(userId, leaveType.LeaveTypeId, year);
            }

            // Get or create balance
            var balance = GetOrCreateBalance(userId, leaveType.LeaveTypeId, year);
            if (balance == null)
                return;

            decimal accrualAmount = 0;

            if (leaveType.AccrualType == "daily")
            {
                // Daily accrual (e.g., Ordinary: 0.1 per day = 1 day per 10 days)
                // Check monthly cap
                decimal accruedThisMonth = GetAccruedThisMonth(userId, leaveType.LeaveTypeId, year, month);

                if (leaveType.AccrualCapMonthly.HasValue &&
                    accruedThisMonth >= leaveType.AccrualCapMonthly.Value)
                {
                    // Already at monthly cap
                    return;
                }

                accrualAmount = leaveType.AccrualRate;

                // Apply monthly cap
                if (leaveType.AccrualCapMonthly.HasValue)
                {
                    decimal remaining = leaveType.AccrualCapMonthly.Value - accruedThisMonth;
                    accrualAmount = Math.Min(accrualAmount, remaining);
                }
            }
            else if (leaveType.AccrualType == "monthly" && isFirstDayOfMonth)
            {
                // Monthly accrual (e.g., Sick Full: 2.5, Sick Half: 3.75)
                accrualAmount = leaveType.AccrualRate;

                // Apply annual cap for non-cumulative types
                if (leaveType.AnnualMax.HasValue)
                {
                    decimal currentTotal = balance.TotalAccrued + balance.CarriedOver;
                    decimal remaining = leaveType.AnnualMax.Value - currentTotal;
                    accrualAmount = Math.Min(accrualAmount, Math.Max(0, remaining));
                }
            }

            if (accrualAmount > 0)
            {
                // Update balance
                UpdateBalanceAccrual(balance.BalanceId, accrualAmount, today);

                // Log transaction
                LogAccrualTransaction(userId, leaveType.LeaveTypeId, balance.BalanceId,
                    accrualAmount, today, leaveType.LeaveTypeNameAr);

                System.Diagnostics.Debug.WriteLine(
                    $"[LeaveAccrual] User {userId}, Type {leaveType.LeaveTypeCode}: +{accrualAmount:F3}");
            }
        }

        #region Database Operations

        private static LeaveAccrualSettings GetAccrualSettings()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"SELECT setting_id, accrual_enabled, accrual_check_time, last_accrual_run, hours_per_day
                        FROM leave_accrual_settings WHERE setting_id = 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new LeaveAccrualSettings
                            {
                                SettingId = reader.GetInt32(0),
                                AccrualEnabled = reader.GetBoolean(1),
                                AccrualCheckTime = reader.GetTimeSpan(2),
                                LastAccrualRun = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                                HoursPerDay = reader.GetInt32(4)
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] GetSettings error: {ex.Message}");
            }

            return LeaveAccrualSettings.Default;
        }

        private static void UpdateLastAccrualRun(DateTime timestamp)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"UPDATE leave_accrual_settings SET last_accrual_run = @timestamp,
                        updated_at = CURRENT_TIMESTAMP WHERE setting_id = 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("timestamp", timestamp);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] UpdateLastRun error: {ex.Message}");
            }
        }

        private static List<int> GetEligibleUsers()
        {
            var users = new List<int>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"SELECT u.user_id FROM users u
                        WHERE u.is_active = TRUE
                        AND u.user_id NOT IN (
                            SELECT DISTINCT user_id_fk FROM long_term_leave_registry
                            WHERE stop_accruals = TRUE
                            AND start_date <= CURRENT_DATE
                            AND (end_date IS NULL OR end_date >= CURRENT_DATE)
                        )";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] GetEligibleUsers error: {ex.Message}");
            }
            return users;
        }

        private static List<LeaveType> GetLeaveTypes()
        {
            var types = new List<LeaveType>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"SELECT leave_type_id, leave_type_code, leave_type_name_ar, leave_type_name_en,
                        accrual_type, accrual_rate, accrual_cap_monthly, is_cumulative, annual_max,
                        reset_on_year_start, deducts_from_balance
                        FROM leave_types WHERE is_active = TRUE AND accrual_type != 'none'";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new LeaveType
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
                                DeductsFromBalance = reader.GetBoolean(10)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] GetLeaveTypes error: {ex.Message}");
            }
            return types;
        }

        private static LeaveBalance GetOrCreateBalance(int userId, int leaveTypeId, int year)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Try to get existing
                    var selectSql = @"SELECT balance_id, total_accrued, used_days, carried_over, manual_adjustment
                        FROM leave_balances
                        WHERE user_id_fk = @userId AND leave_type_id_fk = @leaveTypeId AND year = @year";

                    using (var cmd = new NpgsqlCommand(selectSql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("leaveTypeId", leaveTypeId);
                        cmd.Parameters.AddWithValue("year", year);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new LeaveBalance
                                {
                                    BalanceId = reader.GetInt32(0),
                                    UserId = userId,
                                    LeaveTypeId = leaveTypeId,
                                    Year = year,
                                    TotalAccrued = reader.GetDecimal(1),
                                    UsedDays = reader.GetDecimal(2),
                                    CarriedOver = reader.GetDecimal(3),
                                    ManualAdjustment = reader.GetDecimal(4)
                                };
                            }
                        }
                    }

                    // Create new
                    var insertSql = @"INSERT INTO leave_balances (user_id_fk, leave_type_id_fk, year,
                        total_accrued, used_days, carried_over, manual_adjustment)
                        VALUES (@userId, @leaveTypeId, @year, 0, 0, 0, 0)
                        RETURNING balance_id";

                    using (var cmd = new NpgsqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("leaveTypeId", leaveTypeId);
                        cmd.Parameters.AddWithValue("year", year);

                        var balanceId = Convert.ToInt32(cmd.ExecuteScalar());
                        return new LeaveBalance
                        {
                            BalanceId = balanceId,
                            UserId = userId,
                            LeaveTypeId = leaveTypeId,
                            Year = year,
                            TotalAccrued = 0,
                            UsedDays = 0,
                            CarriedOver = 0,
                            ManualAdjustment = 0
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] GetOrCreateBalance error: {ex.Message}");
                return null;
            }
        }

        private static decimal GetAccruedThisMonth(int userId, int leaveTypeId, int year, int month)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"SELECT COALESCE(SUM(days_amount), 0)
                        FROM leave_transactions
                        WHERE user_id_fk = @userId
                        AND leave_type_id_fk = @leaveTypeId
                        AND transaction_type = 'accrual'
                        AND EXTRACT(YEAR FROM submission_date) = @year
                        AND EXTRACT(MONTH FROM submission_date) = @month";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("leaveTypeId", leaveTypeId);
                        cmd.Parameters.AddWithValue("year", year);
                        cmd.Parameters.AddWithValue("month", month);

                        return Convert.ToDecimal(cmd.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        private static void UpdateBalanceAccrual(int balanceId, decimal amount, DateTime date)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"UPDATE leave_balances SET
                        total_accrued = total_accrued + @amount,
                        last_accrual_date = @date,
                        updated_at = CURRENT_TIMESTAMP
                        WHERE balance_id = @balanceId";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("balanceId", balanceId);
                        cmd.Parameters.AddWithValue("amount", amount);
                        cmd.Parameters.AddWithValue("date", date);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] UpdateBalance error: {ex.Message}");
            }
        }

        private static void ResetBalanceForYear(int userId, int leaveTypeId, int year)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Create or reset balance for new year
                    var sql = @"INSERT INTO leave_balances (user_id_fk, leave_type_id_fk, year,
                        total_accrued, used_days, carried_over, manual_adjustment)
                        VALUES (@userId, @leaveTypeId, @year, 0, 0, 0, 0)
                        ON CONFLICT (user_id_fk, leave_type_id_fk, year) DO UPDATE SET
                        total_accrued = 0, used_days = 0, carried_over = 0, manual_adjustment = 0,
                        updated_at = CURRENT_TIMESTAMP";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("leaveTypeId", leaveTypeId);
                        cmd.Parameters.AddWithValue("year", year);
                        cmd.ExecuteNonQuery();
                    }

                    // Log reset transaction
                    LogAccrualTransaction(userId, leaveTypeId, null, 0, DateTime.Today, "Reset");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] ResetBalance error: {ex.Message}");
            }
        }

        private static void LogAccrualTransaction(int userId, int leaveTypeId, int? balanceId,
            decimal amount, DateTime date, string leaveTypeName)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"INSERT INTO leave_transactions (user_id_fk, leave_type_id_fk, balance_id_fk,
                        transaction_type, days_amount, submission_date, reason)
                        VALUES (@userId, @leaveTypeId, @balanceId, @transType, @amount, @date, @reason)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("leaveTypeId", leaveTypeId);
                        cmd.Parameters.AddWithValue("balanceId", (object)balanceId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("transType", amount == 0 ? "reset" : "accrual");
                        cmd.Parameters.AddWithValue("amount", amount);
                        cmd.Parameters.AddWithValue("date", date);
                        cmd.Parameters.AddWithValue("reason",
                            amount == 0 ? $"تصفير رصيد بداية السنة - {leaveTypeName}"
                                       : $"استحقاق يومي - {leaveTypeName}");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveAccrual] LogTransaction error: {ex.Message}");
            }
        }

        #endregion
    }
}
