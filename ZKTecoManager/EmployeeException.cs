using System;

namespace ZKTecoManager
{
    public class EmployeeException
    {
        public int ExceptionId { get; set; }
        public int UserId { get; set; }
        public int ExceptionTypeId { get; set; }
        public DateTime ExceptionDate { get; set; }
        public string Notes { get; set; }
        public string ExceptionName { get; set; }

        // NEW PROPERTIES FOR OVERRIDES
        public TimeSpan? ClockInOverride { get; set; }
        public TimeSpan? ClockOutOverride { get; set; }
    }
}

