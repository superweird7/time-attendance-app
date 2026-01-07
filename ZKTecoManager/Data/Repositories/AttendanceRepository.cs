using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository implementation for Attendance Log operations.
    /// </summary>
    public class AttendanceRepository : BaseRepository, IAttendanceRepository
    {
        public async Task<List<AttendanceLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, List<int> departmentIds = null)
        {
            return await ExecuteAsync(async conn =>
            {
                var logs = new List<AttendanceLog>();
                var sql = @"
                    SELECT a.log_id, a.user_badge_number, a.log_time, a.machine_id,
                           u.name, d.dept_name, u.user_id
                    FROM attendance_logs a
                    LEFT JOIN users u ON a.user_badge_number = u.badge_number
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    WHERE a.log_time >= @startDate AND a.log_time < @endDate";

                if (departmentIds != null && departmentIds.Count > 0)
                {
                    sql += " AND u.default_dept_id = ANY(@deptIds)";
                }

                sql += " ORDER BY a.log_time";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("startDate", startDate);
                    cmd.Parameters.AddWithValue("endDate", endDate.AddDays(1));

                    if (departmentIds != null && departmentIds.Count > 0)
                    {
                        cmd.Parameters.AddWithValue("deptIds", departmentIds.ToArray());
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add(MapAttendanceLog(reader));
                        }
                    }
                }
                return logs;
            });
        }

        public async Task<List<AttendanceLog>> GetByUserAndDateRangeAsync(string badgeNumber, DateTime startDate, DateTime endDate)
        {
            return await ExecuteAsync(async conn =>
            {
                var logs = new List<AttendanceLog>();
                var sql = @"
                    SELECT a.log_id, a.user_badge_number, a.log_time, a.machine_id,
                           u.name, d.dept_name, u.user_id
                    FROM attendance_logs a
                    LEFT JOIN users u ON a.user_badge_number = u.badge_number
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    WHERE a.user_badge_number = @badge
                      AND a.log_time >= @startDate AND a.log_time < @endDate
                    ORDER BY a.log_time";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("badge", badgeNumber);
                    cmd.Parameters.AddWithValue("startDate", startDate);
                    cmd.Parameters.AddWithValue("endDate", endDate.AddDays(1));

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add(MapAttendanceLog(reader));
                        }
                    }
                }
                return logs;
            });
        }

        public async Task<int> AddBulkAsync(List<AttendanceLog> logs)
        {
            if (logs == null || logs.Count == 0) return 0;

            return await ExecuteAsync(async conn =>
            {
                int count = 0;
                var sql = @"
                    INSERT INTO attendance_logs (user_badge_number, log_time, machine_id)
                    VALUES (@badge, @logTime, @machineId)
                    ON CONFLICT DO NOTHING";

                foreach (var log in logs)
                {
                    // Normalize badge number - remove leading zeros to match user table format
                    var normalizedBadge = log.UserBadgeNumber?.TrimStart('0') ?? "0";
                    if (string.IsNullOrEmpty(normalizedBadge)) normalizedBadge = "0";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("badge", normalizedBadge);
                        cmd.Parameters.AddWithValue("logTime", log.LogTime);
                        cmd.Parameters.AddWithValue("machineId", log.MachineId);
                        count += await cmd.ExecuteNonQueryAsync();
                    }
                }
                return count;
            });
        }

        public async Task<AttendanceLog> GetLatestForUserOnDateAsync(string badgeNumber, DateTime date)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"
                    SELECT a.log_id, a.user_badge_number, a.log_time, a.machine_id,
                           u.name, d.dept_name, u.user_id
                    FROM attendance_logs a
                    LEFT JOIN users u ON a.user_badge_number = u.badge_number
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    WHERE a.user_badge_number = @badge
                      AND DATE(a.log_time) = @date
                    ORDER BY a.log_time DESC
                    LIMIT 1";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("badge", badgeNumber);
                    cmd.Parameters.AddWithValue("date", date.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapAttendanceLog(reader);
                        }
                    }
                }
                return null;
            });
        }

        public async Task<List<string>> GetPresentBadgesOnDateAsync(DateTime date)
        {
            return await ExecuteAsync(async conn =>
            {
                var badges = new List<string>();
                var sql = @"
                    SELECT DISTINCT user_badge_number
                    FROM attendance_logs
                    WHERE DATE(log_time) = @date";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("date", date.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            badges.Add(reader.GetString(0));
                        }
                    }
                }
                return badges;
            });
        }

        public async Task<Dictionary<int, int>> GetAttendanceCountByDepartmentAsync(DateTime startDate, DateTime endDate)
        {
            return await ExecuteAsync(async conn =>
            {
                var counts = new Dictionary<int, int>();
                var sql = @"
                    SELECT u.default_dept_id, COUNT(DISTINCT a.user_badge_number)
                    FROM attendance_logs a
                    INNER JOIN users u ON a.user_badge_number = u.badge_number
                    WHERE a.log_time >= @startDate AND a.log_time < @endDate
                    GROUP BY u.default_dept_id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("startDate", startDate);
                    cmd.Parameters.AddWithValue("endDate", endDate.AddDays(1));

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                counts[reader.GetInt32(0)] = (int)reader.GetInt64(1);
                            }
                        }
                    }
                }
                return counts;
            });
        }

        public async Task DeleteByMachineAsync(int machineId)
        {
            await ExecuteNonQueryAsync("DELETE FROM attendance_logs WHERE machine_id = @id",
                new NpgsqlParameter("id", machineId));
        }

        private AttendanceLog MapAttendanceLog(NpgsqlDataReader reader)
        {
            return new AttendanceLog
            {
                LogId = reader.GetInt32(0),
                UserBadgeNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                LogTime = reader.GetDateTime(2),
                MachineId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Name = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Departments = reader.IsDBNull(5) ? "" : reader.GetString(5),
                UserId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
            };
        }
    }
}
