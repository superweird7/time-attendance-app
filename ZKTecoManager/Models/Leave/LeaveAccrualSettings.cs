using System;

namespace ZKTecoManager.Models.Leave
{
    /// <summary>
    /// System-wide settings for leave accrual processing
    /// </summary>
    public class LeaveAccrualSettings
    {
        public int SettingId { get; set; }

        /// <summary>
        /// Whether automatic accrual processing is enabled
        /// </summary>
        public bool AccrualEnabled { get; set; }

        /// <summary>
        /// Time of day when the daily accrual check runs
        /// </summary>
        public TimeSpan AccrualCheckTime { get; set; }

        /// <summary>
        /// Timestamp of the last accrual run
        /// </summary>
        public DateTime? LastAccrualRun { get; set; }

        /// <summary>
        /// Number of hours that equal one day for hourly leave conversion
        /// </summary>
        public int HoursPerDay { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Default settings
        /// </summary>
        public static LeaveAccrualSettings Default => new LeaveAccrualSettings
        {
            SettingId = 1,
            AccrualEnabled = true,
            AccrualCheckTime = new TimeSpan(0, 30, 0), // 00:30 AM
            HoursPerDay = 7
        };
    }
}
