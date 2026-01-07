using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public class AuditLogEntry
    {
        public int LogId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string ActionType { get; set; }
        public string TableName { get; set; }
        public int? RecordId { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string IpAddress { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSynced { get; set; }

        public string ActionTypeArabic
        {
            get
            {
                switch (ActionType?.ToUpper())
                {
                    case "INSERT": return "اضافة";
                    case "UPDATE": return "تعديل";
                    case "DELETE": return "حذف";
                    case "LOGIN": return "تسجيل دخول";
                    case "LOGOUT": return "تسجيل خروج";
                    case "SYNC": return "مزامنة";
                    case "BACKUP": return "نسخ احتياطي";
                    case "RESTORE": return "استعادة";
                    default: return ActionType ?? "";
                }
            }
        }

        public string TableNameArabic
        {
            get
            {
                switch (TableName?.ToLower())
                {
                    case "users": return "الموظفين";
                    case "departments": return "الاقسام";
                    case "shifts": return "الدوامات";
                    case "machines": return "الاجهزة";
                    case "attendance_logs": return "سجلات الحضور";
                    case "employee_exceptions": return "الاستثناءات";
                    case "exception_types": return "انواع الاستثناءات";
                    default: return TableName ?? "";
                }
            }
        }

        public string DisplayText => $"[{CreatedAt:yyyy-MM-dd HH:mm:ss}] {UserName} - {ActionTypeArabic} {TableNameArabic}: {Description}";
    }

    public static class AuditLogger
    {
        private static readonly object _fileLock = new object();
        private static string _changesFilePath;

        public static string ChangesFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_changesFilePath))
                {
                    var appFolder = AppDomain.CurrentDomain.BaseDirectory;
                    _changesFilePath = Path.Combine(appFolder, "changes_log.txt");
                }
                return _changesFilePath;
            }
        }

        public static void Log(string actionType, string tableName, int? recordId, string oldValue, string newValue, string description)
        {
            var timestamp = DateTime.Now;
            var userName = CurrentUser.Name ?? "Unknown";
            var ipAddress = GetLocalIPAddress();

            // Save to database
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var sql = @"INSERT INTO audit_logs
                                (user_id, action_type, table_name, record_id, old_value, new_value, ip_address, description, is_synced)
                                VALUES (@userId, @actionType, @tableName, @recordId, @oldValue, @newValue, @ip, @description, false)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", CurrentUser.UserId);
                        cmd.Parameters.AddWithValue("actionType", actionType);
                        cmd.Parameters.AddWithValue("tableName", (object)tableName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("recordId", (object)recordId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("oldValue", (object)oldValue ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("newValue", (object)newValue ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("ip", ipAddress);
                        cmd.Parameters.AddWithValue("description", (object)description ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audit log to DB failed: {ex.Message}");
            }

            // Save to text file
            SaveToFile(timestamp, userName, actionType, tableName, recordId, oldValue, newValue, description, ipAddress);
        }

        private static void SaveToFile(DateTime timestamp, string userName, string actionType, string tableName,
            int? recordId, string oldValue, string newValue, string description, string ipAddress)
        {
            try
            {
                lock (_fileLock)
                {
                    var logLine = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] User: {userName} | Action: {actionType} | Table: {tableName} | RecordId: {recordId} | Description: {description}";

                    if (!string.IsNullOrEmpty(oldValue))
                        logLine += $" | OldValue: {oldValue}";
                    if (!string.IsNullOrEmpty(newValue))
                        logLine += $" | NewValue: {newValue}";

                    logLine += $" | IP: {ipAddress}";

                    File.AppendAllText(ChangesFilePath, logLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audit log to file failed: {ex.Message}");
            }
        }

        public static List<AuditLogEntry> GetRecentChanges(int limit = 100, bool unSyncedOnly = false)
        {
            var logs = new List<AuditLogEntry>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var sql = @"SELECT al.log_id, al.user_id, u.name, al.action_type, al.table_name,
                                       al.record_id, al.old_value, al.new_value, al.ip_address,
                                       al.description, al.created_at, COALESCE(al.is_synced, false)
                                FROM audit_logs al
                                LEFT JOIN users u ON al.user_id = u.user_id";

                    if (unSyncedOnly)
                        sql += " WHERE COALESCE(al.is_synced, false) = false";

                    sql += " ORDER BY al.created_at DESC LIMIT @limit";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("limit", limit);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                logs.Add(new AuditLogEntry
                                {
                                    LogId = reader.GetInt32(0),
                                    UserId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                    UserName = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                                    ActionType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    TableName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    RecordId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                                    OldValue = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    NewValue = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    IpAddress = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    CreatedAt = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                    IsSynced = reader.GetBoolean(11)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get audit logs failed: {ex.Message}");
            }
            return logs;
        }

        public static void MarkAsSynced(List<int> logIds)
        {
            if (logIds == null || logIds.Count == 0) return;

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "UPDATE audit_logs SET is_synced = true WHERE log_id = ANY(@ids)";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("ids", logIds.ToArray());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mark synced failed: {ex.Message}");
            }
        }

        public static string GetChangesFileContent()
        {
            try
            {
                if (File.Exists(ChangesFilePath))
                {
                    return File.ReadAllText(ChangesFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Read changes file failed: {ex.Message}");
            }
            return "";
        }

        public static void ClearChangesFile()
        {
            try
            {
                lock (_fileLock)
                {
                    if (File.Exists(ChangesFilePath))
                    {
                        File.WriteAllText(ChangesFilePath, "");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clear changes file failed: {ex.Message}");
            }
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
