using System;

namespace ZKTecoManager.Models.Leave
{
    /// <summary>
    /// Represents a long-term leave entry (5-year leave, study leave, etc.)
    /// that stops accruals for the employee during the leave period.
    /// </summary>
    public class LongTermLeaveEntry
    {
        public int RegistryId { get; set; }
        public int UserId { get; set; }

        /// <summary>
        /// Type of long-term leave: five_year, study, unpaid_extended
        /// </summary>
        public string LeaveType { get; set; }

        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of the leave. NULL means ongoing.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Whether all accruals should stop during this leave
        /// </summary>
        public bool StopAccruals { get; set; }

        public string Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties (populated from joins)
        public string EmployeeName { get; set; }
        public string BadgeNumber { get; set; }
        public string CreatedByName { get; set; }

        /// <summary>
        /// Whether the leave is currently active (no end date or end date in future)
        /// </summary>
        public bool IsActive => !EndDate.HasValue || EndDate.Value >= DateTime.Today;

        /// <summary>
        /// Leave type display name
        /// </summary>
        public string LeaveTypeDisplay
        {
            get
            {
                switch (LeaveType)
                {
                    case "five_year": return "اجازة خمس سنوات - 5-Year Leave";
                    case "study": return "اجازة دراسية - Study Leave";
                    case "unpaid_extended": return "اجازة بدون راتب ممتدة - Extended Unpaid Leave";
                    default: return LeaveType;
                }
            }
        }
    }
}
