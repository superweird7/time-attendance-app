using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Models.Leave;
using ZKTecoManager.Services.Interfaces;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// Service implementation for leave management business logic
    /// </summary>
    public class LeaveManagementService : ILeaveManagementService
    {
        private readonly ILeaveRepository _leaveRepository;
        private readonly IUserRepository _userRepository;
        private const int HOURS_PER_DAY = 7;
        private const int MAX_UNPAID_DAYS_PER_MONTH = 5;

        public LeaveManagementService(ILeaveRepository leaveRepository, IUserRepository userRepository)
        {
            _leaveRepository = leaveRepository;
            _userRepository = userRepository;
        }

        #region Balance Operations

        public async Task<List<LeaveBalance>> GetEmployeeBalancesAsync(int userId, int year)
        {
            // Ensure balances are initialized
            await _leaveRepository.InitializeBalancesForUserAsync(userId, year);
            return await _leaveRepository.GetBalancesByUserAsync(userId, year);
        }

        public async Task<LeaveBalance> GetBalanceAsync(int userId, int leaveTypeId, int year)
        {
            return await _leaveRepository.GetBalanceAsync(userId, leaveTypeId, year);
        }

        public async Task<(bool Success, string Message)> AdjustBalanceAsync(
            int userId, int leaveTypeId, int year, decimal adjustment, string reason, int createdBy)
        {
            try
            {
                // Get or create the balance
                var balance = await _leaveRepository.GetBalanceAsync(userId, leaveTypeId, year);
                if (balance == null)
                {
                    await _leaveRepository.InitializeBalancesForUserAsync(userId, year);
                    balance = await _leaveRepository.GetBalanceAsync(userId, leaveTypeId, year);
                }

                if (balance == null)
                    return (false, "فشل في العثور على رصيد الاجازة - Failed to find leave balance");

                // Update the manual adjustment
                balance.ManualAdjustment += adjustment;
                await _leaveRepository.UpdateBalanceAsync(balance);

                // Log the transaction
                await _leaveRepository.AddTransactionAsync(new LeaveTransaction
                {
                    UserId = userId,
                    LeaveTypeId = leaveTypeId,
                    BalanceId = balance.BalanceId,
                    TransactionType = "adjustment",
                    DaysAmount = adjustment,
                    SubmissionDate = DateTime.Today,
                    Reason = reason,
                    CreatedBy = createdBy
                });

                var adjustType = adjustment >= 0 ? "اضافة" : "خصم";
                return (true, $"تم {adjustType} {Math.Abs(adjustment)} يوم بنجاح - Successfully adjusted {Math.Abs(adjustment)} days");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveManagement] AdjustBalance error: {ex.Message}");
                return (false, $"خطأ: {ex.Message}");
            }
        }

        public async Task InitializeUserBalancesAsync(int userId)
        {
            await _leaveRepository.InitializeBalancesForUserAsync(userId, DateTime.Today.Year);
        }

        #endregion

        #region Leave Deduction

        public async Task<(bool Success, string Message)> DeductLeaveAsync(
            int userId, int leaveTypeId, DateTime startDate, DateTime endDate,
            string reason, int createdBy)
        {
            try
            {
                // Calculate number of days
                decimal days = (decimal)(endDate - startDate).TotalDays + 1;

                // Validate the deduction
                var validation = await ValidateLeaveDeductionAsync(userId, leaveTypeId, days, startDate);
                if (!validation.IsValid)
                    return (false, validation.Message);

                // Get the leave type
                var leaveType = await _leaveRepository.GetLeaveTypeByIdAsync(leaveTypeId);
                if (leaveType == null)
                    return (false, "نوع الاجازة غير موجود - Leave type not found");

                // Get or create the balance for the year
                int year = startDate.Year;
                var balance = await _leaveRepository.GetBalanceAsync(userId, leaveTypeId, year);
                if (balance == null)
                {
                    await _leaveRepository.InitializeBalancesForUserAsync(userId, year);
                    balance = await _leaveRepository.GetBalanceAsync(userId, leaveTypeId, year);
                }

                if (balance == null)
                    return (false, "فشل في العثور على رصيد الاجازة - Failed to find leave balance");

                // Update the used days
                balance.UsedDays += days;
                await _leaveRepository.UpdateBalanceAsync(balance);

                // Log the transaction
                await _leaveRepository.AddTransactionAsync(new LeaveTransaction
                {
                    UserId = userId,
                    LeaveTypeId = leaveTypeId,
                    BalanceId = balance.BalanceId,
                    TransactionType = "deduction",
                    DaysAmount = -days, // Negative for deduction
                    StartDate = startDate,
                    EndDate = endDate,
                    SubmissionDate = DateTime.Today,
                    Reason = reason,
                    CreatedBy = createdBy
                });

                return (true, $"تم تسجيل {days} يوم اجازة بنجاح - Successfully recorded {days} days of leave");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveManagement] DeductLeave error: {ex.Message}");
                return (false, $"خطأ: {ex.Message}");
            }
        }

        public async Task<(bool IsValid, string Message)> ValidateLeaveDeductionAsync(
            int userId, int leaveTypeId, decimal days, DateTime startDate)
        {
            var leaveType = await _leaveRepository.GetLeaveTypeByIdAsync(leaveTypeId);
            if (leaveType == null)
                return (false, "نوع الاجازة غير موجود - Leave type not found");

            // Check unpaid leave monthly limit
            if (leaveType.LeaveTypeCode == "UNPAID")
            {
                var withinLimit = await IsUnpaidLeaveWithinLimitAsync(
                    userId, startDate.Year, startDate.Month, days);
                if (!withinLimit)
                    return (false, $"تجاوز الحد الاقصى للاجازة بدون راتب ({MAX_UNPAID_DAYS_PER_MONTH} ايام/شهر) - Exceeds unpaid leave limit ({MAX_UNPAID_DAYS_PER_MONTH} days/month)");
            }

            // Check if deducts from balance and has sufficient balance
            if (leaveType.DeductsFromBalance)
            {
                var balance = await _leaveRepository.GetBalanceAsync(userId, leaveTypeId, startDate.Year);
                if (balance == null)
                {
                    await _leaveRepository.InitializeBalancesForUserAsync(userId, startDate.Year);
                    balance = await _leaveRepository.GetBalanceAsync(userId, leaveTypeId, startDate.Year);
                }

                if (balance != null && balance.RemainingDays < days)
                {
                    return (false, $"رصيد الاجازة غير كاف. المتبقي: {balance.RemainingDays:F1} يوم - Insufficient balance. Remaining: {balance.RemainingDays:F1} days");
                }
            }

            // Check if on long-term leave
            var onLongTermLeave = await IsOnLongTermLeaveAsync(userId);
            if (onLongTermLeave)
                return (false, "الموظف في اجازة طويلة الامد - Employee is on long-term leave");

            return (true, "OK");
        }

        #endregion

        #region Hourly Leave

        public async Task<(bool Success, string Message)> AddHourlyLeaveAsync(
            int userId, decimal hours, DateTime submissionDate, string reason, int createdBy)
        {
            try
            {
                if (hours <= 0)
                    return (false, "يجب ادخال عدد ساعات صحيح - Please enter a valid number of hours");

                if (hours > HOURS_PER_DAY)
                    return (false, $"الحد الاقصى للاجازة الزمنية {HOURS_PER_DAY} ساعات - Maximum hourly leave is {HOURS_PER_DAY} hours");

                // Get or create accumulator
                var accumulator = await _leaveRepository.GetHourlyAccumulatorAsync(userId);
                if (accumulator == null)
                {
                    accumulator = new HourlyLeaveAccumulator
                    {
                        UserId = userId,
                        AccumulatedHours = 0,
                        TotalHoursConverted = 0,
                        TotalDaysDeducted = 0
                    };
                }

                // Add hours
                accumulator.AccumulatedHours += hours;

                // Check for conversion to days
                int daysToDeduct = (int)(accumulator.AccumulatedHours / HOURS_PER_DAY);
                decimal remainderHours = accumulator.AccumulatedHours % HOURS_PER_DAY;

                string message;
                if (daysToDeduct > 0)
                {
                    // Get ORDINARY leave type
                    var ordinaryType = await _leaveRepository.GetLeaveTypeByCodeAsync("ORDINARY");
                    if (ordinaryType == null)
                        return (false, "نوع الاجازة الاعتيادية غير موجود - Ordinary leave type not found");

                    // Get balance
                    int year = submissionDate.Year;
                    var balance = await _leaveRepository.GetBalanceAsync(userId, ordinaryType.LeaveTypeId, year);
                    if (balance == null)
                    {
                        await _leaveRepository.InitializeBalancesForUserAsync(userId, year);
                        balance = await _leaveRepository.GetBalanceAsync(userId, ordinaryType.LeaveTypeId, year);
                    }

                    if (balance == null || balance.RemainingDays < daysToDeduct)
                    {
                        return (false, $"رصيد الاجازة الاعتيادية غير كاف للتحويل. المتبقي: {balance?.RemainingDays ?? 0:F1} - Insufficient ordinary balance. Remaining: {balance?.RemainingDays ?? 0:F1}");
                    }

                    // Deduct from balance
                    balance.UsedDays += daysToDeduct;
                    await _leaveRepository.UpdateBalanceAsync(balance);

                    // Log conversion transaction
                    await _leaveRepository.AddTransactionAsync(new LeaveTransaction
                    {
                        UserId = userId,
                        LeaveTypeId = ordinaryType.LeaveTypeId,
                        BalanceId = balance.BalanceId,
                        TransactionType = "hourly_conversion",
                        DaysAmount = -daysToDeduct,
                        HoursAmount = daysToDeduct * HOURS_PER_DAY,
                        SubmissionDate = submissionDate,
                        Reason = $"تحويل {daysToDeduct * HOURS_PER_DAY} ساعة اجازة زمنية الى {daysToDeduct} يوم",
                        Notes = reason,
                        CreatedBy = createdBy
                    });

                    // Update accumulator stats
                    accumulator.TotalHoursConverted += daysToDeduct * HOURS_PER_DAY;
                    accumulator.TotalDaysDeducted += daysToDeduct;
                    accumulator.LastConversionDate = submissionDate;

                    message = $"تم تسجيل {hours} ساعة. تم تحويل {daysToDeduct * HOURS_PER_DAY} ساعة الى {daysToDeduct} يوم. المتبقي: {remainderHours} ساعة";
                }
                else
                {
                    message = $"تم تسجيل {hours} ساعة. المجموع المتراكم: {accumulator.AccumulatedHours} ساعة ({HOURS_PER_DAY - accumulator.AccumulatedHours} ساعة متبقية حتى التحويل)";
                }

                // Update accumulator with remainder
                accumulator.AccumulatedHours = remainderHours;
                await _leaveRepository.CreateOrUpdateHourlyAccumulatorAsync(accumulator);

                return (true, message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveManagement] AddHourlyLeave error: {ex.Message}");
                return (false, $"خطأ: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> AddHourlyLeaveByTimeRangeAsync(
            int userId, TimeSpan startTime, TimeSpan endTime,
            DateTime submissionDate, string reason, int createdBy)
        {
            // Calculate hours from time range
            TimeSpan duration;
            if (endTime > startTime)
                duration = endTime - startTime;
            else
                duration = TimeSpan.FromHours(24) - startTime + endTime;

            decimal hours = (decimal)duration.TotalHours;

            return await AddHourlyLeaveAsync(userId, hours, submissionDate, reason, createdBy);
        }

        public async Task<HourlyLeaveAccumulator> GetHourlyAccumulatorAsync(int userId)
        {
            return await _leaveRepository.GetHourlyAccumulatorAsync(userId);
        }

        #endregion

        #region Validation

        public async Task<bool> IsUnpaidLeaveWithinLimitAsync(int userId, int year, int month, decimal additionalDays)
        {
            var unpaidType = await _leaveRepository.GetLeaveTypeByCodeAsync("UNPAID");
            if (unpaidType == null)
                return true; // If type doesn't exist, don't block

            var usedThisMonth = await _leaveRepository.GetUsedDaysInMonthAsync(
                userId, unpaidType.LeaveTypeId, year, month);

            return (usedThisMonth + additionalDays) <= MAX_UNPAID_DAYS_PER_MONTH;
        }

        public async Task<bool> IsOnLongTermLeaveAsync(int userId)
        {
            var longTermLeave = await _leaveRepository.GetActiveLongTermLeaveByUserAsync(userId);
            return longTermLeave != null;
        }

        #endregion

        #region Long-Term Leave

        public async Task<(bool Success, string Message)> StartLongTermLeaveAsync(
            int userId, string leaveType, DateTime startDate, string notes, int createdBy)
        {
            try
            {
                // Check if already on long-term leave
                var existing = await _leaveRepository.GetActiveLongTermLeaveByUserAsync(userId);
                if (existing != null)
                    return (false, "الموظف في اجازة طويلة الامد حالياً - Employee is already on long-term leave");

                await _leaveRepository.AddLongTermLeaveAsync(new LongTermLeaveEntry
                {
                    UserId = userId,
                    LeaveType = leaveType,
                    StartDate = startDate,
                    StopAccruals = true,
                    Notes = notes,
                    CreatedBy = createdBy
                });

                return (true, "تم تسجيل الاجازة الطويلة بنجاح - Long-term leave recorded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveManagement] StartLongTermLeave error: {ex.Message}");
                return (false, $"خطأ: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> EndLongTermLeaveAsync(int registryId, DateTime endDate)
        {
            try
            {
                await _leaveRepository.EndLongTermLeaveAsync(registryId, endDate);
                return (true, "تم انهاء الاجازة الطويلة بنجاح - Long-term leave ended successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LeaveManagement] EndLongTermLeave error: {ex.Message}");
                return (false, $"خطأ: {ex.Message}");
            }
        }

        public async Task<List<LongTermLeaveEntry>> GetActiveLongTermLeavesAsync()
        {
            return await _leaveRepository.GetActiveLongTermLeavesAsync();
        }

        #endregion

        #region Reports

        public async Task<List<LeaveTransaction>> GetLeaveHistoryAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            return await _leaveRepository.GetTransactionsByUserAsync(userId, startDate, endDate);
        }

        public async Task<List<LeaveTransaction>> GetAllTransactionsAsync(DateTime startDate, DateTime endDate, List<int> departmentIds = null)
        {
            return await _leaveRepository.GetAllTransactionsAsync(startDate, endDate, null, departmentIds);
        }

        public async Task<List<LeaveBalance>> GetDepartmentBalancesAsync(int year, List<int> departmentIds = null)
        {
            return await _leaveRepository.GetAllBalancesAsync(year, null, departmentIds);
        }

        #endregion

        #region Leave Types

        public async Task<List<LeaveType>> GetLeaveTypesAsync()
        {
            return await _leaveRepository.GetAllLeaveTypesAsync(true);
        }

        public async Task<LeaveType> GetLeaveTypeByCodeAsync(string code)
        {
            return await _leaveRepository.GetLeaveTypeByCodeAsync(code);
        }

        #endregion
    }
}
