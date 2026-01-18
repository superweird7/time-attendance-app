using System;

namespace ZKTecoManager.Models.Leave
{
    /// <summary>
    /// Represents a type of leave with its accrual configuration
    /// </summary>
    public class LeaveType
    {
        public int LeaveTypeId { get; set; }
        public string LeaveTypeCode { get; set; }
        public string LeaveTypeNameAr { get; set; }
        public string LeaveTypeNameEn { get; set; }

        /// <summary>
        /// Accrual type: "none", "daily", "monthly"
        /// </summary>
        public string AccrualType { get; set; }

        /// <summary>
        /// Accrual rate per period (e.g., 0.1 for 1 day per 10 days)
        /// </summary>
        public decimal AccrualRate { get; set; }

        /// <summary>
        /// Maximum days that can be accrued per month
        /// </summary>
        public decimal? AccrualCapMonthly { get; set; }

        /// <summary>
        /// Whether balance can carry over to next year
        /// </summary>
        public bool IsCumulative { get; set; }

        /// <summary>
        /// Maximum days allowed per year (e.g., 45 for Sick Half Pay)
        /// </summary>
        public decimal? AnnualMax { get; set; }

        /// <summary>
        /// Whether balance resets to 0 on January 1st
        /// </summary>
        public bool ResetOnYearStart { get; set; }

        /// <summary>
        /// Maximum days allowed per month (e.g., 5 for Unpaid)
        /// </summary>
        public int? MaxDaysPerMonth { get; set; }

        /// <summary>
        /// Whether this leave type deducts from balance
        /// </summary>
        public bool DeductsFromBalance { get; set; }

        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Display name for UI (Arabic - English)
        /// </summary>
        public string DisplayName => $"{LeaveTypeNameAr} - {LeaveTypeNameEn}";

        /// <summary>
        /// Whether this is a long-term leave type that stops accruals
        /// </summary>
        public bool IsLongTermLeave => LeaveTypeCode == "FIVE_YEAR" || LeaveTypeCode == "STUDY";
    }
}
