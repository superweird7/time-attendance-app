namespace ZKTecoManager.Models.Dashboard
{
    /// <summary>
    /// Information about an absent employee for dashboard display.
    /// </summary>
    public class AbsenteeInfo
    {
        /// <summary>
        /// Employee's badge number.
        /// </summary>
        public string BadgeNumber { get; set; }

        /// <summary>
        /// Employee's full name.
        /// </summary>
        public string EmployeeName { get; set; }

        /// <summary>
        /// Department name.
        /// </summary>
        public string DepartmentName { get; set; }

        /// <summary>
        /// Shift name assigned to the employee.
        /// </summary>
        public string ShiftName { get; set; }

        /// <summary>
        /// Number of consecutive days absent (for trend tracking).
        /// </summary>
        public int ConsecutiveDaysAbsent { get; set; }
    }
}
