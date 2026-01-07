namespace ZKTecoManager.Models.Dashboard
{
    /// <summary>
    /// Contains attendance KPI data for the dashboard.
    /// </summary>
    public class AttendanceKpiData
    {
        /// <summary>
        /// Total number of employees with assigned shifts.
        /// </summary>
        public int TotalEmployees { get; set; }

        /// <summary>
        /// Number of employees present today (have at least one attendance log).
        /// </summary>
        public int PresentToday { get; set; }

        /// <summary>
        /// Number of employees absent today (no attendance and no leave exception).
        /// </summary>
        public int AbsentToday { get; set; }

        /// <summary>
        /// Number of employees who arrived late today.
        /// </summary>
        public int LateArrivals { get; set; }

        /// <summary>
        /// Number of employees on approved leave today.
        /// </summary>
        public int OnLeave { get; set; }

        /// <summary>
        /// Attendance rate as a percentage (Present / Total * 100).
        /// </summary>
        public decimal AttendanceRate { get; set; }

        /// <summary>
        /// Calculates the attendance rate based on current values.
        /// </summary>
        public void CalculateAttendanceRate()
        {
            if (TotalEmployees > 0)
            {
                AttendanceRate = ((decimal)(PresentToday + OnLeave) / TotalEmployees) * 100;
            }
            else
            {
                AttendanceRate = 0;
            }
        }
    }
}
