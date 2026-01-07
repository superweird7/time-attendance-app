using System;

namespace ZKTecoManager.Models.Dashboard
{
    /// <summary>
    /// Information about a late arrival for dashboard display.
    /// </summary>
    public class LateArrivalInfo
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
        /// Expected arrival time based on shift.
        /// </summary>
        public TimeSpan ExpectedTime { get; set; }

        /// <summary>
        /// Actual arrival time.
        /// </summary>
        public TimeSpan ActualTime { get; set; }

        /// <summary>
        /// Minutes late.
        /// </summary>
        public int MinutesLate { get; set; }

        /// <summary>
        /// Formatted string showing how late the employee was.
        /// </summary>
        public string LateBy
        {
            get
            {
                if (MinutesLate < 60)
                    return $"{MinutesLate} min";

                int hours = MinutesLate / 60;
                int mins = MinutesLate % 60;
                return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
            }
        }
    }
}
