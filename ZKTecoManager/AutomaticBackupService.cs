using System;
using System.IO;
using System.Threading;
using Npgsql;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public class AutomaticBackupService
    {
        private static Timer _backupTimer;
        private static volatile bool _isRunning = false;
        private static readonly object _lockObject = new object();

        public static void Start()
        {
            lock (_lockObject)
            {
                if (_isRunning) return;
                _isRunning = true;
            }

            // ✅ Check every 1 hour for production
            _backupTimer = new Timer(CheckAndBackup, null, TimeSpan.FromSeconds(10), TimeSpan.FromHours(1));

            System.Diagnostics.Debug.WriteLine($"[AutoBackup] Service started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"[AutoBackup] Will check every hour for local and server backups");
        }

        public static void Stop()
        {
            _backupTimer?.Dispose();
            _isRunning = false;
            System.Diagnostics.Debug.WriteLine($"[AutoBackup] Service stopped at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        private static void CheckAndBackup(object state)
        {
            try
            {
                // Get backup settings
                bool autoBackupEnabled = false;
                TimeSpan backupTime = new TimeSpan(10, 0, 0); // Default 10:00 AM
                DateTime? lastBackup = null;
                int retentionDays = 30;

                // Server backup settings
                bool serverBackupEnabled = false;
                string serverBackupPath = null;
                int serverBackupIntervalDays = 10;
                DateTime? lastServerBackup = null;

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Check if table exists
                    var checkTableSql = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'backup_settings')";
                    using (var checkCmd = new NpgsqlCommand(checkTableSql, conn))
                    {
                        bool tableExists = (bool)checkCmd.ExecuteScalar();
                        if (!tableExists)
                        {
                            System.Diagnostics.Debug.WriteLine("[AutoBackup] ⚠️ backup_settings table does not exist!");
                            return;
                        }
                    }

                    var sql = @"SELECT auto_backup_enabled, backup_time, last_backup_date, backup_retention_days,
                                       server_backup_enabled, server_backup_path, server_backup_interval_days, last_server_backup_date
                                FROM backup_settings LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                autoBackupEnabled = reader.GetBoolean(0);
                                backupTime = reader.GetTimeSpan(1);
                                lastBackup = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                                retentionDays = reader.GetInt32(3);

                                // Server backup settings - use ordinal checks to handle missing columns
                                try
                                {
                                    serverBackupEnabled = !reader.IsDBNull(4) && reader.GetBoolean(4);
                                    serverBackupPath = reader.IsDBNull(5) ? null : reader.GetString(5);
                                    serverBackupIntervalDays = reader.IsDBNull(6) ? 10 : reader.GetInt32(6);
                                    lastServerBackup = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7);
                                }
                                catch
                                {
                                    // Columns may not exist in older database versions
                                    serverBackupEnabled = false;
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[AutoBackup] ⚠️ No settings found in backup_settings table!");
                                return;
                            }
                        }
                    }
                }

                var now = DateTime.Now;

                // ======== Local Backup Logic ========
                if (autoBackupEnabled)
                {
                    bool shouldBackup = false;

                    if (lastBackup == null)
                    {
                        shouldBackup = true;
                        System.Diagnostics.Debug.WriteLine("[AutoBackup] 📦 No previous local backup found - creating first backup");
                    }
                    else if (lastBackup.Value.Date < now.Date && now.TimeOfDay >= backupTime)
                    {
                        shouldBackup = true;
                        System.Diagnostics.Debug.WriteLine($"[AutoBackup] 📦 Local backup due - Last backup: {lastBackup.Value:yyyy-MM-dd HH:mm}");
                    }

                    if (shouldBackup)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoBackup] 🚀 Starting automatic local backup at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                        bool success = BackupManager.CreateBackup();

                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("[AutoBackup] ✅ Local backup completed successfully!");

                            // Delete old backups
                            BackupManager.DeleteOldBackups(retentionDays);
                            System.Diagnostics.Debug.WriteLine($"[AutoBackup] 🧹 Cleaned up local backups older than {retentionDays} days");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[AutoBackup] ❌ Local backup failed!");
                        }
                    }
                }

                // ======== Server Backup Logic ========
                if (serverBackupEnabled && !string.IsNullOrWhiteSpace(serverBackupPath))
                {
                    bool shouldServerBackup = false;

                    if (lastServerBackup == null)
                    {
                        shouldServerBackup = true;
                        System.Diagnostics.Debug.WriteLine("[AutoBackup] 🖥️ No previous server backup found - creating first server backup");
                    }
                    else
                    {
                        // Check if enough days have passed since last server backup
                        int daysSinceLastBackup = (now.Date - lastServerBackup.Value.Date).Days;
                        if (daysSinceLastBackup >= serverBackupIntervalDays)
                        {
                            shouldServerBackup = true;
                            System.Diagnostics.Debug.WriteLine($"[AutoBackup] 🖥️ Server backup due - Last backup: {lastServerBackup.Value:yyyy-MM-dd} ({daysSinceLastBackup} days ago)");
                        }
                    }

                    if (shouldServerBackup)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoBackup] 🚀 Starting automatic server backup to {serverBackupPath}");

                        bool success = CreateServerBackup(serverBackupPath);

                        if (success)
                        {
                            // Update last server backup date
                            UpdateLastServerBackupDate();
                            System.Diagnostics.Debug.WriteLine("[AutoBackup] ✅ Server backup completed successfully!");

                            // Clean old server backups
                            DeleteOldServerBackups(serverBackupPath, retentionDays);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[AutoBackup] ❌ Server backup failed!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoBackup] ❌ Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a backup to the server network share path
        /// </summary>
        private static bool CreateServerBackup(string serverPath)
        {
            try
            {
                // Ensure the server path exists
                if (!Directory.Exists(serverPath))
                {
                    try
                    {
                        Directory.CreateDirectory(serverPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoBackup] Cannot access server path: {ex.Message}");
                        return false;
                    }
                }

                // Generate backup filename with timestamp and machine name
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string machineName = Environment.MachineName;
                string backupFile = Path.Combine(serverPath, $"zkteco_backup_{machineName}_{timestamp}.zkbak");

                // Use BackupManager to create the backup to server path
                return BackupManager.CreateBackupSilent(backupFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoBackup] Server backup error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates the last server backup date in database
        /// </summary>
        private static void UpdateLastServerBackupDate()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "UPDATE backup_settings SET last_server_backup_date = @date";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("date", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoBackup] Error updating last server backup date: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes old backups from server path
        /// </summary>
        private static void DeleteOldServerBackups(string serverPath, int retentionDays)
        {
            try
            {
                if (!Directory.Exists(serverPath)) return;

                var files = Directory.GetFiles(serverPath, "zkteco_backup_*.zkbak");
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            File.Delete(file);
                            System.Diagnostics.Debug.WriteLine($"[AutoBackup] 🧹 Deleted old server backup: {file}");
                        }
                    }
                    catch (Exception fileEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoBackup] Error deleting old server backup {file}: {fileEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoBackup] Delete old server backups failed: {ex.Message}");
            }
        }
    }
}
