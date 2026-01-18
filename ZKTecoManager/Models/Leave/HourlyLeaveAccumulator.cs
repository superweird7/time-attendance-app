using System;

namespace ZKTecoManager.Models.Leave
{
    /// <summary>
    /// Tracks accumulated hourly leave for an employee.
    /// When accumulated hours reach 7, they are converted to 1 day of ordinary leave.
    /// </summary>
    public class HourlyLeaveAccumulator
    {
        public int AccumulatorId { get; set; }
        public int UserId { get; set; }

        /// <summary>
        /// Current accumulated hours (0-6.99). Resets to remainder after conversion.
        /// </summary>
        public decimal AccumulatedHours { get; set; }

        /// <summary>
        /// Date when the last 7-hour block was converted to a day
        /// </summary>
        public DateTime? LastConversionDate { get; set; }

        /// <summary>
        /// Historical total hours that have been converted to days
        /// </summary>
        public decimal TotalHoursConverted { get; set; }

        /// <summary>
        /// Historical total days deducted from hourly leave conversions
        /// </summary>
        public int TotalDaysDeducted { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties (populated from joins)
        public string EmployeeName { get; set; }
        public string BadgeNumber { get; set; }

        /// <summary>
        /// Hours remaining until next day conversion
        /// </summary>
        public decimal HoursUntilNextDay => 7 - AccumulatedHours;
    }
}
