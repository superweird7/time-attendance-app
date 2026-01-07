using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Data.Repositories;
using ZKTecoManager.Services;
using ZKTecoManager.Services.Interfaces;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// Simple service locator for dependency injection.
    /// Provides singleton instances of repositories and services.
    /// </summary>
    public static class ServiceLocator
    {
        // Repository instances (lazy initialization)
        private static IDepartmentRepository _departmentRepository;
        private static IUserRepository _userRepository;
        private static IShiftRepository _shiftRepository;
        private static IAttendanceRepository _attendanceRepository;
        private static IExceptionRepository _exceptionRepository;
        private static IMachineRepository _machineRepository;

        // Service instances (lazy initialization)
        private static IDashboardService _dashboardService;

        #region Repositories

        public static IDepartmentRepository DepartmentRepository =>
            _departmentRepository ?? (_departmentRepository = new DepartmentRepository());

        public static IUserRepository UserRepository =>
            _userRepository ?? (_userRepository = new UserRepository());

        public static IShiftRepository ShiftRepository =>
            _shiftRepository ?? (_shiftRepository = new ShiftRepository());

        public static IAttendanceRepository AttendanceRepository =>
            _attendanceRepository ?? (_attendanceRepository = new AttendanceRepository());

        public static IExceptionRepository ExceptionRepository =>
            _exceptionRepository ?? (_exceptionRepository = new ExceptionRepository());

        public static IMachineRepository MachineRepository =>
            _machineRepository ?? (_machineRepository = new MachineRepository());

        #endregion

        #region Services

        public static IDashboardService DashboardService =>
            _dashboardService ?? (_dashboardService = new DashboardService(
                UserRepository, AttendanceRepository, ExceptionRepository, ShiftRepository));

        #endregion

        /// <summary>
        /// Resets all cached instances. Useful for testing or reconfiguration.
        /// </summary>
        public static void Reset()
        {
            _departmentRepository = null;
            _userRepository = null;
            _shiftRepository = null;
            _attendanceRepository = null;
            _exceptionRepository = null;
            _machineRepository = null;
            _dashboardService = null;
        }
    }
}
