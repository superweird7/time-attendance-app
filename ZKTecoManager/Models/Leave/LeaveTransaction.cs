using System;

namespace ZKTecoManager.Models.Leave
{
    /// <summary>
    /// Represents a leave transaction (deduction, accrual, adjustment, etc.)
    /// </summary>
    public class LeaveTransaction
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public int LeaveTypeId { get; set; }
        public int? BalanceId { get; set; }

        /// <summary>
        /// Transaction type: deduction, accrual, adjustment, carryover, hourly_conversion, reset
        /// </summary>
        public string TransactionType { get; set; }

        /// <summary>
        /// Days amount (positive for additions, negative for deductions)
        /// </summary>
        public decimal DaysAmount { get; set; }

        /// <summary>
        /// Hours amount (for hourly leave tracking)
        /// </summary>
        public decimal? HoursAmount { get; set; }

        /// <summary>
        /// Leave start date (for deductions)
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Leave end date (for deductions)
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Date the transaction was submitted/recorded
        /// </summary>
        public DateTime SubmissionDate { get; set; }

        public string Reason { get; set; }
        public string Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation/display properties (populated from joins)
        public string EmployeeName { get; set; }
        public string BadgeNumber { get; set; }
        public string LeaveTypeName { get; set; }
        public string LeaveTypeNameAr { get; set; }
        public string LeaveTypeNameEn { get; set; }
        public string CreatedByName { get; set; }

        /// <summary>
        /// Formatted transaction type for display
        /// </summary>
        public string TransactionTypeDisplay
        {
            get
            {
                switch (TransactionType)
                {
                    case "deduction": return "خصم - Deduction";
                    case "accrual": return "استحقاق - Accrual";
                    case "adjustment": return "تعديل - Adjustment";
                    case "carryover": return "ترحيل - Carryover";
                    case "hourly_conversion": return "تحويل ساعات - Hourly Conversion";
                    case "reset": return "تصفير - Reset";
                    default: return TransactionType;
                }
            }
        }

        /// <summary>
        /// Formatted date range for display
        /// </summary>
        public string DateRangeDisplay
        {
            get
            {
                if (StartDate.HasValue && EndDate.HasValue)
                {
                    if (StartDate.Value == EndDate.Value)
                        return StartDate.Value.ToString("yyyy-MM-dd");
                    return $"{StartDate.Value:yyyy-MM-dd} - {EndDate.Value:yyyy-MM-dd}";
                }
                return SubmissionDate.ToString("yyyy-MM-dd");
            }
        }
    }
}
