using System;

namespace ZKTecoManager.Models.Leave
{
    /// <summary>
    /// Represents an employee's leave balance for a specific leave type and year
    /// </summary>
    public class LeaveBalance
    {
        public int BalanceId { get; set; }
        public int UserId { get; set; }
        public int LeaveTypeId { get; set; }
        public int Year { get; set; }

        /// <summary>
        /// Total days accrued this year (from daily/monthly accrual)
        /// </summary>
        public decimal TotalAccrued { get; set; }

        /// <summary>
        /// Total days used this year
        /// </summary>
        public decimal UsedDays { get; set; }

        /// <summary>
        /// Days carried over from previous year (for cumulative types)
        /// </summary>
        public decimal CarriedOver { get; set; }

        /// <summary>
        /// Manual adjustments made by admin (+/-)
        /// </summary>
        public decimal ManualAdjustment { get; set; }

        /// <summary>
        /// Last date when accrual was processed
        /// </summary>
        public DateTime? LastAccrualDate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Calculated remaining balance (CarriedOver + TotalAccrued + ManualAdjustment - UsedDays)
        /// </summary>
        public decimal RemainingDays => CarriedOver + TotalAccrued + ManualAdjustment - UsedDays;

        // Navigation/display properties (populated from joins)
        public string EmployeeName { get; set; }
        public string BadgeNumber { get; set; }
        public string DepartmentName { get; set; }
        public string LeaveTypeName { get; set; }
        public string LeaveTypeCode { get; set; }
        public string LeaveTypeNameAr { get; set; }
        public string LeaveTypeNameEn { get; set; }
    }
}
