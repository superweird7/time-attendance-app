using Npgsql;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public static class DataValidator
    {

        // Validate badge number format
        public static bool IsValidBadgeNumber(string badgeNumber)
        {
            if (string.IsNullOrWhiteSpace(badgeNumber))
                return false;

            // Badge number should be alphanumeric, 1-20 characters
            return Regex.IsMatch(badgeNumber, @"^[a-zA-Z0-9]{1,20}$");
        }

        // Check if badge number already exists
        public static bool IsBadgeNumberUnique(string badgeNumber, int? excludeUserId = null)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    string sql = "SELECT COUNT(*) FROM users WHERE badge_number = @badge";
                    if (excludeUserId.HasValue)
                    {
                        sql += " AND user_id != @userId";
                    }

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badgeNumber);
                        if (excludeUserId.HasValue)
                        {
                            cmd.Parameters.AddWithValue("userId", excludeUserId.Value);
                        }

                        var count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking badge uniqueness: {ex.Message}");
                return false;
            }
        }

        // Validate email format
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return true; // Email is optional

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Validate IP address
        public static bool IsValidIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            return Regex.IsMatch(ipAddress,
                @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
        }

        // Validate time format (HH:mm or HH:mm:ss)
        public static bool IsValidTimeFormat(string time)
        {
            if (string.IsNullOrWhiteSpace(time))
                return false;

            return TimeSpan.TryParse(time, out _);
        }

        // Check for duplicate attendance log
        public static bool IsDuplicateAttendanceLog(string badgeNumber, DateTime logTime, int machineId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var sql = @"SELECT COUNT(*) FROM attendance_logs 
                               WHERE user_badge_number = @badge 
                               AND log_time = @time 
                               AND machine_id = @machineId";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badgeNumber);
                        cmd.Parameters.AddWithValue("time", logTime);
                        cmd.Parameters.AddWithValue("machineId", machineId);

                        var count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking duplicate attendance log: {ex.Message}");
                return false;
            }
        }
    }
}
