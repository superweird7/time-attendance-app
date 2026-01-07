using System;

namespace ZKTecoManager
{
    public class AttendanceLog
    {
        public int LogId { get; set; }
        public string UserBadgeNumber { get; set; }
        public DateTime LogTime { get; set; }
        public int MachineId { get; set; }
        public string Name { get; set; }
        public string Departments { get; set; }

        // Added properties for reporting
        public int UserId { get; set; }
        public TimeSpan? RequiredClockIn { get; set; }
        public TimeSpan? RequiredClockOut { get; set; }
    }
}