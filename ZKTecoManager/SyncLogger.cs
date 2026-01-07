using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZKTecoManager.Models.Sync;

namespace ZKTecoManager
{
    /// <summary>
    /// Logs sync operations to a text file in the SyncLogs folder
    /// </summary>
    public static class SyncLogger
    {
        private static readonly string LogsFolder;
        private static readonly object _lock = new object();

        static SyncLogger()
        {
            // Create SyncLogs folder in the application directory
            LogsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyncLogs");
            if (!Directory.Exists(LogsFolder))
            {
                Directory.CreateDirectory(LogsFolder);
            }
        }

        /// <summary>
        /// Gets the path to the current log file (one file per day)
        /// </summary>
        public static string CurrentLogFilePath => Path.Combine(LogsFolder, $"sync_{DateTime.Now:yyyy-MM-dd}.txt");

        /// <summary>
        /// Logs a sync operation result
        /// </summary>
        public static void LogSyncResult(string locationName, SyncResult result, List<PendingChange> appliedChanges = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"  تاريخ ووقت المزامنة: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  الموقع: {locationName}");
            sb.AppendLine($"  المستخدم: {CurrentUser.Name}");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");
            sb.AppendLine($"  الحالة: {(result.Success ? "نجاح" : "فشل")}");
            sb.AppendLine($"  السجلات المضافة: {result.RecordsAdded}");
            sb.AppendLine($"  السجلات المحدثة: {result.RecordsUpdated}");
            sb.AppendLine($"  السجلات المتخطاة: {result.RecordsSkipped}");

            if (result.Errors != null && result.Errors.Count > 0)
            {
                sb.AppendLine("───────────────────────────────────────────────────────────────────");
                sb.AppendLine("  الأخطاء:");
                foreach (var error in result.Errors)
                {
                    sb.AppendLine($"    - {error}");
                }
            }

            if (appliedChanges != null && appliedChanges.Count > 0)
            {
                sb.AppendLine("───────────────────────────────────────────────────────────────────");
                sb.AppendLine("  التغييرات المطبقة:");
                foreach (var change in appliedChanges)
                {
                    var changeTypeAr = GetChangeTypeArabic(change.ChangeType);
                    var tableNameAr = GetTableNameArabic(change.TableName);
                    sb.AppendLine($"    [{changeTypeAr}] {tableNameAr}: {change.RecordDescription}");
                }
            }

            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            WriteToFile(sb.ToString());
        }

        /// <summary>
        /// Logs a sync start event
        /// </summary>
        public static void LogSyncStart(string locationName, int pendingChangesCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] بدء المزامنة مع: {locationName}");
            sb.AppendLine($"  عدد التغييرات المعلقة: {pendingChangesCount}");

            WriteToFile(sb.ToString());
        }

        /// <summary>
        /// Logs when no changes are found
        /// </summary>
        public static void LogNoChanges(string locationName)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {locationName}: لا توجد تغييرات جديدة";
            WriteToFile(line + Environment.NewLine);
        }

        /// <summary>
        /// Logs a connection test result
        /// </summary>
        public static void LogConnectionTest(string locationName, bool success, string errorMessage = null)
        {
            var sb = new StringBuilder();
            sb.Append($"[{DateTime.Now:HH:mm:ss}] اختبار الاتصال بـ {locationName}: ");
            sb.AppendLine(success ? "نجاح" : $"فشل - {errorMessage}");

            WriteToFile(sb.ToString());
        }

        /// <summary>
        /// Gets all log files in the SyncLogs folder
        /// </summary>
        public static List<string> GetLogFiles()
        {
            var files = new List<string>();
            if (Directory.Exists(LogsFolder))
            {
                files.AddRange(Directory.GetFiles(LogsFolder, "sync_*.txt"));
            }
            return files;
        }

        /// <summary>
        /// Opens the SyncLogs folder in Windows Explorer
        /// </summary>
        public static void OpenLogsFolder()
        {
            if (!Directory.Exists(LogsFolder))
            {
                Directory.CreateDirectory(LogsFolder);
            }
            System.Diagnostics.Process.Start("explorer.exe", LogsFolder);
        }

        /// <summary>
        /// Opens a specific log file
        /// </summary>
        public static void OpenLogFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
        }

        private static void WriteToFile(string content)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(CurrentLogFilePath, content, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to sync log: {ex.Message}");
            }
        }

        private static string GetChangeTypeArabic(ChangeType changeType)
        {
            switch (changeType)
            {
                case ChangeType.New: return "جديد";
                case ChangeType.Updated: return "تحديث";
                case ChangeType.Conflict: return "تعارض";
                default: return changeType.ToString();
            }
        }

        private static string GetTableNameArabic(string tableName)
        {
            switch (tableName?.ToLower())
            {
                case "users": return "الموظفين";
                case "departments": return "الأقسام";
                case "shifts": return "الورديات";
                case "machines": return "الأجهزة";
                case "attendance_logs": return "سجلات الحضور";
                case "employee_exceptions": return "استثناءات الموظفين";
                case "exception_types": return "أنواع الاستثناءات";
                default: return tableName ?? "";
            }
        }
    }
}
