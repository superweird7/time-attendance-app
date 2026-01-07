using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Models.Dashboard;
using ZKTecoManager.Services.Interfaces;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// Service implementation for Dashboard operations.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IExceptionRepository _exceptionRepository;
        private readonly IShiftRepository _shiftRepository;

        public DashboardService(
            IUserRepository userRepository,
            IAttendanceRepository attendanceRepository,
            IExceptionRepository exceptionRepository,
            IShiftRepository shiftRepository)
        {
            _userRepository = userRepository;
            _attendanceRepository = attendanceRepository;
            _exceptionRepository = exceptionRepository;
            _shiftRepository = shiftRepository;
        }

        public async Task<AttendanceKpiData> GetKpiDataAsync(DateTime date, List<int> departmentIds = null)
        {
            var kpi = new AttendanceKpiData();

            // Get all users with shifts
            var allUsers = await _userRepository.GetUsersWithShiftsAsync();

            // Filter by department if specified
            if (departmentIds != null && departmentIds.Count > 0)
            {
                allUsers = allUsers.Where(u => departmentIds.Contains(u.DefaultDeptId)).ToList();
            }

            kpi.TotalEmployees = allUsers.Count;

            if (kpi.TotalEmployees == 0)
            {
                return kpi;
            }

            // Get present badges for the date
            var presentBadges = await _attendanceRepository.GetPresentBadgesOnDateAsync(date);
            var presentBadgesSet = new HashSet<string>(presentBadges);

            // Count present users
            kpi.PresentToday = allUsers.Count(u => presentBadgesSet.Contains(u.BadgeNumber));

            // Get on leave count
            kpi.OnLeave = await _exceptionRepository.GetOnLeaveCountAsync(date, departmentIds);

            // Calculate absent (those who are not present and not on leave)
            var onLeaveUserIds = new HashSet<int>();
            var exceptions = await _exceptionRepository.GetByDateRangeAsync(date, date, departmentIds);
            foreach (var exc in exceptions)
            {
                onLeaveUserIds.Add(exc.UserId);
            }

            kpi.AbsentToday = allUsers.Count(u =>
                !presentBadgesSet.Contains(u.BadgeNumber) &&
                !onLeaveUserIds.Contains(u.UserId));

            // Calculate late arrivals
            var lateArrivals = await GetLateArrivalsAsync(date, departmentIds);
            kpi.LateArrivals = lateArrivals.Count;

            // Calculate attendance rate
            kpi.CalculateAttendanceRate();

            return kpi;
        }

        public async Task<List<AbsenteeInfo>> GetAbsenteesAsync(DateTime date, List<int> departmentIds = null)
        {
            var absentees = new List<AbsenteeInfo>();

            // Get all users with shifts
            var allUsers = await _userRepository.GetUsersWithShiftsAsync();

            // Filter by department if specified
            if (departmentIds != null && departmentIds.Count > 0)
            {
                allUsers = allUsers.Where(u => departmentIds.Contains(u.DefaultDeptId)).ToList();
            }

            // Get present badges for the date
            var presentBadges = await _attendanceRepository.GetPresentBadgesOnDateAsync(date);
            var presentBadgesSet = new HashSet<string>(presentBadges);

            // Get exceptions for the date (users on leave)
            var exceptions = await _exceptionRepository.GetByDateRangeAsync(date, date, departmentIds);
            var onLeaveUserIds = new HashSet<int>(exceptions.Select(e => e.UserId));

            // Get all departments for name lookup
            var departments = await Infrastructure.ServiceLocator.DepartmentRepository.GetAllAsync();
            var deptLookup = departments.ToDictionary(d => d.DeptId, d => d.DeptName);

            // Find absent users (not present and not on leave)
            // Exclude departments containing "نقل" from absent list
            foreach (var user in allUsers)
            {
                if (!presentBadgesSet.Contains(user.BadgeNumber) && !onLeaveUserIds.Contains(user.UserId))
                {
                    var deptName = deptLookup.TryGetValue(user.DefaultDeptId, out var name) ? name : "Unknown";

                    // Skip departments containing "نقل" (transfer departments)
                    if (deptName.Contains("نقل"))
                        continue;

                    absentees.Add(new AbsenteeInfo
                    {
                        BadgeNumber = user.BadgeNumber,
                        EmployeeName = user.Name,
                        DepartmentName = deptName,
                        ShiftName = user.ShiftName ?? "No Shift"
                    });
                }
            }

            return absentees.OrderBy(a => a.EmployeeName).ToList();
        }

        public async Task<List<LateArrivalInfo>> GetLateArrivalsAsync(DateTime date, List<int> departmentIds = null)
        {
            var lateArrivals = new List<LateArrivalInfo>();

            // Get all users with shifts
            var allUsers = await _userRepository.GetUsersWithShiftsAsync();

            // Filter by department if specified
            if (departmentIds != null && departmentIds.Count > 0)
            {
                allUsers = allUsers.Where(u => departmentIds.Contains(u.DefaultDeptId)).ToList();
            }

            // Get all shifts with their rules
            var shifts = await _shiftRepository.GetAllAsync();
            var shiftLookup = shifts.ToDictionary(s => s.ShiftId);

            // Get all departments for name lookup
            var departments = await Infrastructure.ServiceLocator.DepartmentRepository.GetAllAsync();
            var deptLookup = departments.ToDictionary(d => d.DeptId, d => d.DeptName);

            // Get attendance logs for the date
            var logs = await _attendanceRepository.GetByDateRangeAsync(date, date, departmentIds);

            // Group logs by user to get first punch
            var firstPunchByUser = logs
                .GroupBy(l => l.UserBadgeNumber)
                .ToDictionary(g => g.Key, g => g.OrderBy(l => l.LogTime).First().LogTime.TimeOfDay);

            // Grace period (1 minute)
            var gracePeriod = TimeSpan.FromMinutes(1);

            foreach (var user in allUsers)
            {
                if (!user.ShiftId.HasValue || !firstPunchByUser.TryGetValue(user.BadgeNumber, out var actualTime))
                    continue;

                if (!shiftLookup.TryGetValue(user.ShiftId.Value, out var shift))
                    continue;

                // Get expected start time from shift rules (first rule) or StartTime
                TimeSpan expectedTime;
                if (shift.Rules != null && shift.Rules.Count > 0)
                {
                    expectedTime = shift.Rules[0]; // First rule is typically clock-in time
                }
                else
                {
                    expectedTime = shift.StartTime;
                }

                // Check if late (past expected time + grace period)
                var lateThreshold = expectedTime.Add(gracePeriod);
                if (actualTime > lateThreshold)
                {
                    var minutesLate = (int)(actualTime - expectedTime).TotalMinutes;
                    lateArrivals.Add(new LateArrivalInfo
                    {
                        BadgeNumber = user.BadgeNumber,
                        EmployeeName = user.Name,
                        DepartmentName = deptLookup.TryGetValue(user.DefaultDeptId, out var deptName) ? deptName : "Unknown",
                        ExpectedTime = expectedTime,
                        ActualTime = actualTime,
                        MinutesLate = minutesLate
                    });
                }
            }

            return lateArrivals.OrderByDescending(l => l.MinutesLate).ToList();
        }
    }
}
