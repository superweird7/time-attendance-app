using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public static class BackupManager
    {
        // Current schema version for compatibility checking
        private const string SCHEMA_VERSION = "2.0";

        // Whitelist of valid table names to prevent SQL injection
        // Order is important: parent tables first, then child tables
        private static readonly HashSet<string> ValidTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "departments", "shifts", "exception_types", "machines",
            "users", "shift_rules", "biometric_data",
            "attendance_logs", "employee_exceptions",
            "admin_department_mappings", "admin_device_mappings",
            "user_department_permissions",
            "audit_logs", "backup_settings",
            "remote_locations", "sync_settings", "sync_history"
        };

        // Tables in correct restore order (parent tables first)
        private static readonly string[] RestoreOrder = new[]
        {
            "departments",           // No dependencies
            "shifts",                // No dependencies
            "exception_types",       // No dependencies
            "machines",              // No dependencies
            "backup_settings",       // No dependencies
            "sync_settings",         // No dependencies
            "remote_locations",      // No dependencies
            "users",                 // Depends on departments, shifts
            "shift_rules",           // Depends on shifts
            "biometric_data",        // Depends on users
            "attendance_logs",       // Depends on machines
            "employee_exceptions",   // Depends on users, exception_types
            "admin_department_mappings",  // Depends on users, departments
            "admin_device_mappings",      // Depends on users, machines
            "user_department_permissions", // Depends on users, departments
            "audit_logs",            // Depends on users
            "sync_history"           // Depends on remote_locations
        };

        /// <summary>
        /// Creates a backup silently (for automatic backups - no MessageBox)
        /// </summary>
        public static bool CreateBackupSilent(string backupFilePath)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(backupFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                StringBuilder sqlBackup = new StringBuilder();

                // Write header with metadata
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine("-- ZKTeco Manager Database Backup");
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine($"-- SCHEMA_VERSION: {SCHEMA_VERSION}");
                sqlBackup.AppendLine($"-- CREATED_AT: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sqlBackup.AppendLine($"-- MACHINE_NAME: {Environment.MachineName}");
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine();
                sqlBackup.AppendLine("-- This backup file is portable and can be restored on any PC");
                sqlBackup.AppendLine("-- with ZKTeco Manager installed.");
                sqlBackup.AppendLine();

                int totalRecords = 0;

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Backup tables in the correct order for proper restoration
                    foreach (var table in RestoreOrder)
                    {
                        // Check if table exists
                        var existsSql = $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{table}')";
                        bool tableExists = false;
                        using (var existsCmd = new NpgsqlCommand(existsSql, conn))
                        {
                            tableExists = (bool)existsCmd.ExecuteScalar();
                        }

                        if (!tableExists)
                        {
                            continue;
                        }

                        sqlBackup.AppendLine($"\n-- =====================================================");
                        sqlBackup.AppendLine($"-- TABLE: {table}");
                        sqlBackup.AppendLine($"-- =====================================================");

                        int tableRecords = 0;

                        var dataSql = $"SELECT * FROM \"{table}\"";
                        using (var dataCmd = new NpgsqlCommand(dataSql, conn))
                        {
                            dataCmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                            using (var dataReader = dataCmd.ExecuteReader())
                            {
                                var columnNames = new List<string>();
                                var seenColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                for (int i = 0; i < dataReader.FieldCount; i++)
                                {
                                    var colName = dataReader.GetName(i);
                                    if (!seenColumns.Contains(colName))
                                    {
                                        columnNames.Add(colName);
                                        seenColumns.Add(colName);
                                    }
                                }

                                while (dataReader.Read())
                                {
                                    var values = new List<string>();

                                    for (int i = 0; i < columnNames.Count; i++)
                                    {
                                        int ordinal = dataReader.GetOrdinal(columnNames[i]);
                                        if (dataReader.IsDBNull(ordinal))
                                        {
                                            values.Add("NULL");
                                        }
                                        else
                                        {
                                            var rawValue = dataReader.GetValue(ordinal);
                                            string formattedValue = FormatValueForSql(rawValue);
                                            values.Add(formattedValue);
                                        }
                                    }

                                    if (values.Count > 0)
                                    {
                                        sqlBackup.AppendLine(
                                            $"INSERT INTO \"{table}\" ({string.Join(", ", columnNames.ConvertAll(c => $"\"{c}\""))}) " +
                                            $"VALUES ({string.Join(", ", values)});"
                                        );
                                        tableRecords++;
                                    }
                                }
                            }
                        }

                        sqlBackup.AppendLine($"-- Records: {tableRecords}");
                        totalRecords += tableRecords;
                    }
                }

                // Write footer
                sqlBackup.AppendLine();
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine($"-- BACKUP COMPLETE: {totalRecords} total records");
                sqlBackup.AppendLine("-- =====================================================");

                // Write to file with UTF-8 BOM for proper Arabic support
                File.WriteAllText(backupFilePath, sqlBackup.ToString(), new UTF8Encoding(true));

                // Log the backup
                try
                {
                    AuditLogger.Log("BACKUP", null, null, null, backupFilePath, $"Server backup created: {backupFilePath} ({totalRecords} records)");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Audit log failed: {logEx.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Silent backup error: {ex.Message}");
                return false;
            }
        }

        public static bool CreateBackup(string backupPath = null)
        {
            try
            {
                // Get backup settings
                if (string.IsNullOrEmpty(backupPath))
                {
                    backupPath = GetBackupPath();
                }

                // Create backup directory if not exists
                Directory.CreateDirectory(backupPath);

                // Generate backup filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupPath, $"zkteco_backup_{timestamp}.zkbak");

                StringBuilder sqlBackup = new StringBuilder();

                // Write header with metadata
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine("-- ZKTeco Manager Database Backup");
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine($"-- SCHEMA_VERSION: {SCHEMA_VERSION}");
                sqlBackup.AppendLine($"-- CREATED_AT: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sqlBackup.AppendLine($"-- MACHINE_NAME: {Environment.MachineName}");
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine();
                sqlBackup.AppendLine("-- This backup file is portable and can be restored on any PC");
                sqlBackup.AppendLine("-- with ZKTeco Manager installed.");
                sqlBackup.AppendLine();

                int totalRecords = 0;

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Backup tables in the correct order for proper restoration
                    foreach (var table in RestoreOrder)
                    {
                        // Check if table exists
                        var existsSql = $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{table}')";
                        bool tableExists = false;
                        using (var existsCmd = new NpgsqlCommand(existsSql, conn))
                        {
                            tableExists = (bool)existsCmd.ExecuteScalar();
                        }

                        if (!tableExists)
                        {
                            System.Diagnostics.Debug.WriteLine($"Table {table} does not exist, skipping");
                            continue;
                        }

                        sqlBackup.AppendLine($"\n-- =====================================================");
                        sqlBackup.AppendLine($"-- TABLE: {table}");
                        sqlBackup.AppendLine($"-- =====================================================");

                        int tableRecords = 0;

                        // Use quoted identifier for safety
                        var dataSql = $"SELECT * FROM \"{table}\"";
                        using (var dataCmd = new NpgsqlCommand(dataSql, conn))
                        {
                            dataCmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                            using (var dataReader = dataCmd.ExecuteReader())
                            {
                                // Get column info once
                                var columnNames = new List<string>();
                                var seenColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                for (int i = 0; i < dataReader.FieldCount; i++)
                                {
                                    var colName = dataReader.GetName(i);
                                    if (!seenColumns.Contains(colName))
                                    {
                                        columnNames.Add(colName);
                                        seenColumns.Add(colName);
                                    }
                                }

                                while (dataReader.Read())
                                {
                                    var values = new List<string>();

                                    for (int i = 0; i < columnNames.Count; i++)
                                    {
                                        int ordinal = dataReader.GetOrdinal(columnNames[i]);
                                        if (dataReader.IsDBNull(ordinal))
                                        {
                                            values.Add("NULL");
                                        }
                                        else
                                        {
                                            var rawValue = dataReader.GetValue(ordinal);
                                            string formattedValue = FormatValueForSql(rawValue);
                                            values.Add(formattedValue);
                                        }
                                    }

                                    if (values.Count > 0)
                                    {
                                        sqlBackup.AppendLine(
                                            $"INSERT INTO \"{table}\" ({string.Join(", ", columnNames.ConvertAll(c => $"\"{c}\""))}) " +
                                            $"VALUES ({string.Join(", ", values)});"
                                        );
                                        tableRecords++;
                                    }
                                }
                            }
                        }

                        sqlBackup.AppendLine($"-- Records: {tableRecords}");
                        totalRecords += tableRecords;
                    }
                }

                // Write footer
                sqlBackup.AppendLine();
                sqlBackup.AppendLine("-- =====================================================");
                sqlBackup.AppendLine($"-- BACKUP COMPLETE: {totalRecords} total records");
                sqlBackup.AppendLine("-- =====================================================");

                // Write to file with UTF-8 BOM for proper Arabic support
                File.WriteAllText(backupFile, sqlBackup.ToString(), new UTF8Encoding(true));

                // Update last backup date
                UpdateLastBackupDate();

                // Log the backup
                try
                {
                    AuditLogger.Log("BACKUP", null, null, null, backupFile, $"Database backup created: {backupFile} ({totalRecords} records)");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Audit log failed: {logEx.Message}");
                }

                MessageBox.Show(
                    $"Backup created successfully!\nتم إنشاء النسخة الاحتياطية بنجاح!\n\n" +
                    $"File: {backupFile}\n" +
                    $"Records: {totalRecords}",
                    "Success / نجاح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return true;
            }
            catch (NpgsqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database backup error: {ex.Message}");
                MessageBox.Show(
                    $"Database error during backup:\n{ex.Message}\n\nخطأ في قاعدة البيانات أثناء النسخ الاحتياطي",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"File I/O backup error: {ex.Message}");
                MessageBox.Show(
                    $"File error during backup:\n{ex.Message}\n\nخطأ في الملفات أثناء النسخ الاحتياطي",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup error: {ex.Message}");
                MessageBox.Show(
                    $"Backup error:\n{ex.Message}\n\nخطأ في النسخ الاحتياطي",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        public static bool RestoreBackup(string backupFile)
        {
            try
            {
                if (!File.Exists(backupFile))
                {
                    MessageBox.Show(
                        "Backup file not found.\nملف النسخة الاحتياطية غير موجود",
                        "Restore Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Read and validate backup file
                string sqlContent = File.ReadAllText(backupFile, Encoding.UTF8);

                // Check if it's a valid backup file
                if (!sqlContent.Contains("ZKTeco Manager Database Backup"))
                {
                    MessageBox.Show(
                        "Invalid backup file format.\nصيغة ملف النسخة الاحتياطية غير صحيحة",
                        "Restore Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Extract backup info for display
                string backupInfo = ExtractBackupInfo(sqlContent);

                var result = MessageBox.Show(
                    "WARNING: This will replace ALL current data with the backup data.\n\n" +
                    "تحذير: سيتم استبدال جميع البيانات الحالية بالبيانات من النسخة الاحتياطية\n\n" +
                    $"{backupInfo}\n\n" +
                    "Are you sure you want to continue?\nهل أنت متأكد من المتابعة؟",
                    "Confirm Restore / تأكيد الاستعادة",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return false;

                // Parse INSERT statements grouped by table
                var tableInserts = ParseInsertStatements(sqlContent);

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // First, ensure all tables exist
                    EnsureTablesExist(conn);

                    // Use transaction for atomicity
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Disable foreign key constraints temporarily
                            using (var cmd = new NpgsqlCommand("SET session_replication_role = replica;", conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // Clear all tables in reverse order (child tables first)
                            for (int i = RestoreOrder.Length - 1; i >= 0; i--)
                            {
                                string table = RestoreOrder[i];
                                try
                                {
                                    using (var cmd = new NpgsqlCommand($"TRUNCATE TABLE \"{table}\" RESTART IDENTITY CASCADE;", conn, transaction))
                                    {
                                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                catch (PostgresException tableEx) when (tableEx.SqlState == "42P01")
                                {
                                    System.Diagnostics.Debug.WriteLine($"Table {table} does not exist, skipping truncate");
                                }
                                catch (Exception tableEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error truncating table {table}: {tableEx.Message}");
                                }
                            }

                            // Insert data in correct order (parent tables first)
                            foreach (var table in RestoreOrder)
                            {
                                if (!tableInserts.ContainsKey(table))
                                    continue;

                                var statements = tableInserts[table];
                                System.Diagnostics.Debug.WriteLine($"Restoring {statements.Count} records to {table}");

                                foreach (var statement in statements)
                                {
                                    try
                                    {
                                        using (var cmd = new NpgsqlCommand(statement + ";", conn, transaction))
                                        {
                                            cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                                            cmd.ExecuteNonQuery();
                                            successCount++;
                                        }
                                    }
                                    catch (PostgresException pgEx) when (pgEx.SqlState == "23505") // Duplicate key
                                    {
                                        // Skip duplicates silently
                                        System.Diagnostics.Debug.WriteLine($"Duplicate key skipped: {pgEx.Message}");
                                    }
                                    catch (Exception stmtEx)
                                    {
                                        errorCount++;
                                        System.Diagnostics.Debug.WriteLine($"Insert error in {table}: {stmtEx.Message}");
                                        if (errors.Count < 10)
                                        {
                                            errors.Add($"[{table}] {stmtEx.Message}");
                                        }
                                    }
                                }
                            }

                            // Re-enable foreign key constraints
                            using (var cmd = new NpgsqlCommand("SET session_replication_role = DEFAULT;", conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // Reset all sequences to match the max IDs in each table
                            ResetAllSequences(conn, transaction);

                            // Ensure default backup_settings exist
                            EnsureBackupSettings(conn, transaction);

                            // Ensure default sync_settings exist
                            EnsureSyncSettings(conn, transaction);

                            // Commit the transaction
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            // Rollback on critical failure
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception rollbackEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Rollback failed: {rollbackEx.Message}");
                            }
                            throw new Exception($"Restore failed: {ex.Message}", ex);
                        }
                    }
                }

                // Log the restore
                try
                {
                    AuditLogger.Log("RESTORE", null, null, null, backupFile,
                        $"Database restored from: {backupFile} (Success: {successCount}, Errors: {errorCount})");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Audit log failed: {logEx.Message}");
                }

                // Show result
                if (errorCount > 0)
                {
                    MessageBox.Show(
                        $"Restore completed with some warnings:\n" +
                        $"تم الاستعادة مع بعض التحذيرات:\n\n" +
                        $"Success / نجاح: {successCount} records\n" +
                        $"Skipped / تخطي: {errorCount} records\n\n" +
                        (errors.Count > 0 ? $"Errors:\n{string.Join("\n", errors.GetRange(0, Math.Min(5, errors.Count)))}" : ""),
                        "Restore Warning / تحذير الاستعادة",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        $"Database restored successfully!\nتم الاستعادة بنجاح!\n\n" +
                        $"Records restored: {successCount}",
                        "Success / نجاح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return true;
            }
            catch (NpgsqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database restore error: {ex.Message}");
                MessageBox.Show(
                    $"Database error during restore:\n{ex.Message}\n\nخطأ في قاعدة البيانات أثناء الاستعادة",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Restore error: {ex.Message}");
                MessageBox.Show(
                    $"Restore error:\n{ex.Message}\n\nخطأ في الاستعادة",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Extract backup metadata for display
        /// </summary>
        private static string ExtractBackupInfo(string sqlContent)
        {
            var info = new StringBuilder();

            var versionMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"SCHEMA_VERSION:\s*(.+)");
            var dateMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"CREATED_AT:\s*(.+)");
            var machineMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"MACHINE_NAME:\s*(.+)");
            var recordsMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"BACKUP COMPLETE:\s*(\d+)");

            if (versionMatch.Success) info.AppendLine($"Version: {versionMatch.Groups[1].Value.Trim()}");
            if (dateMatch.Success) info.AppendLine($"Created: {dateMatch.Groups[1].Value.Trim()}");
            if (machineMatch.Success) info.AppendLine($"From: {machineMatch.Groups[1].Value.Trim()}");
            if (recordsMatch.Success) info.AppendLine($"Records: {recordsMatch.Groups[1].Value.Trim()}");

            return info.ToString();
        }

        /// <summary>
        /// Parse INSERT statements from backup file, grouped by table name
        /// </summary>
        private static Dictionary<string, List<string>> ParseInsertStatements(string sqlContent)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var regex = new System.Text.RegularExpressions.Regex(
                @"INSERT INTO ""(\w+)""\s*\([^)]+\)\s*VALUES\s*\([^;]+\);",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in regex.Matches(sqlContent))
            {
                string tableName = match.Groups[1].Value;
                string statement = match.Value.TrimEnd(';');

                if (!result.ContainsKey(tableName))
                {
                    result[tableName] = new List<string>();
                }
                result[tableName].Add(statement);
            }

            return result;
        }

        /// <summary>
        /// Ensure all required tables exist in the database
        /// </summary>
        private static void EnsureTablesExist(NpgsqlConnection conn)
        {
            // This will create tables if they don't exist
            // For now, we assume the database schema is already created
            // In the future, we could add schema creation here
        }

        /// <summary>
        /// Ensure backup_settings has at least one row
        /// </summary>
        private static void EnsureBackupSettings(NpgsqlConnection conn, NpgsqlTransaction transaction)
        {
            try
            {
                var checkSql = "SELECT COUNT(*) FROM backup_settings";
                long count = 0;
                using (var cmd = new NpgsqlCommand(checkSql, conn, transaction))
                {
                    count = (long)cmd.ExecuteScalar();
                }

                if (count == 0)
                {
                    var insertSql = @"INSERT INTO backup_settings (auto_backup_enabled, backup_time, backup_retention_days, backup_path)
                                     VALUES (TRUE, '02:00:00', 30, 'C:\ZKTecoBackups')";
                    using (var cmd = new NpgsqlCommand(insertSql, conn, transaction))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureBackupSettings error: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensure sync_settings has at least one row
        /// </summary>
        private static void EnsureSyncSettings(NpgsqlConnection conn, NpgsqlTransaction transaction)
        {
            try
            {
                var checkSql = "SELECT COUNT(*) FROM sync_settings";
                long count = 0;
                using (var cmd = new NpgsqlCommand(checkSql, conn, transaction))
                {
                    count = (long)cmd.ExecuteScalar();
                }

                if (count == 0)
                {
                    var insertSql = "INSERT INTO sync_settings (auto_sync_enabled, sync_interval_minutes) VALUES (FALSE, 15)";
                    using (var cmd = new NpgsqlCommand(insertSql, conn, transaction))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureSyncSettings error: {ex.Message}");
            }
        }

        public static string GetBackupPath()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT backup_path FROM backup_settings LIMIT 1";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting backup path: {ex.Message}");
            }
            return @"C:\ZKTecoBackups";
        }

        private static void UpdateLastBackupDate()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "UPDATE backup_settings SET last_backup_date = @date";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                        cmd.Parameters.AddWithValue("date", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating last backup date: {ex.Message}");
            }
        }

        public static void DeleteOldBackups(int retentionDays)
        {
            try
            {
                string backupPath = GetBackupPath();
                if (!Directory.Exists(backupPath)) return;

                var files = Directory.GetFiles(backupPath, "zkteco_backup_*.sql");
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            File.Delete(file);
                            try
                            {
                                AuditLogger.Log("DELETE", null, null, null, file, $"Old backup deleted: {file}");
                            }
                            catch (Exception logEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Audit log failed for deleted backup: {logEx.Message}");
                            }
                        }
                    }
                    catch (Exception fileEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error deleting old backup {file}: {fileEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete old backups failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that a table name is in our whitelist.
        /// </summary>
        private static bool IsValidTableName(string tableName)
        {
            return !string.IsNullOrEmpty(tableName) && ValidTableNames.Contains(tableName);
        }

        /// <summary>
        /// Resets all sequences to match the maximum ID values in their respective tables.
        /// This prevents "duplicate key" errors after restoring a backup.
        /// </summary>
        private static void ResetAllSequences(NpgsqlConnection conn, NpgsqlTransaction transaction)
        {
            // Map of sequence name -> (table name, column name)
            var sequenceMap = new Dictionary<string, (string table, string column)>
            {
                { "departments_dept_id_seq", ("departments", "dept_id") },
                { "shifts_shift_id_seq", ("shifts", "shift_id") },
                { "shift_rules_rule_id_seq", ("shift_rules", "rule_id") },
                { "exception_types_exception_type_id_seq", ("exception_types", "exception_type_id") },
                { "machines_id_seq", ("machines", "id") },
                { "users_user_id_seq", ("users", "user_id") },
                { "attendance_logs_log_id_seq", ("attendance_logs", "log_id") },
                { "biometric_data_biometric_id_seq", ("biometric_data", "biometric_id") },
                { "employee_exceptions_exception_id_seq", ("employee_exceptions", "exception_id") },
                { "admin_department_mappings_mapping_id_seq", ("admin_department_mappings", "mapping_id") },
                { "admin_device_mappings_mapping_id_seq", ("admin_device_mappings", "mapping_id") },
                { "user_department_permissions_permission_id_seq", ("user_department_permissions", "permission_id") },
                { "audit_logs_log_id_seq", ("audit_logs", "log_id") },
                { "backup_settings_setting_id_seq", ("backup_settings", "setting_id") },
                { "remote_locations_location_id_seq", ("remote_locations", "location_id") },
                { "sync_settings_setting_id_seq", ("sync_settings", "setting_id") },
                { "sync_history_sync_id_seq", ("sync_history", "sync_id") }
            };

            foreach (var kvp in sequenceMap)
            {
                try
                {
                    string sequenceName = kvp.Key;
                    string tableName = kvp.Value.table;
                    string columnName = kvp.Value.column;

                    // Check if table exists first
                    var existsSql = $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName}')";
                    bool tableExists = false;
                    using (var existsCmd = new NpgsqlCommand(existsSql, conn, transaction))
                    {
                        tableExists = (bool)existsCmd.ExecuteScalar();
                    }

                    if (!tableExists)
                    {
                        System.Diagnostics.Debug.WriteLine($"Table {tableName} does not exist, skipping sequence reset");
                        continue;
                    }

                    // Get the max value from the table
                    string maxSql = $"SELECT COALESCE(MAX(\"{columnName}\"), 0) FROM \"{tableName}\"";
                    long maxValue = 0;

                    using (var cmd = new NpgsqlCommand(maxSql, conn, transaction))
                    {
                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            maxValue = Convert.ToInt64(result);
                        }
                    }

                    // Set the sequence to max + 1 (or 1 if table is empty)
                    long nextValue = Math.Max(maxValue + 1, 1);
                    string setValSql = $"SELECT setval('{sequenceName}', {nextValue}, false)";
                    using (var cmd = new NpgsqlCommand(setValSql, conn, transaction))
                    {
                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                        cmd.ExecuteNonQuery();
                    }

                    System.Diagnostics.Debug.WriteLine($"Reset sequence {sequenceName} to {nextValue}");
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42883")
                {
                    // Table or sequence doesn't exist - skip silently
                    System.Diagnostics.Debug.WriteLine($"Skipping sequence {kvp.Key}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Log but don't fail the restore for sequence reset errors
                    System.Diagnostics.Debug.WriteLine($"Error resetting sequence {kvp.Key}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Verifies the integrity of a backup file without restoring it.
        /// </summary>
        public static BackupVerificationResult VerifyBackup(string backupFile)
        {
            var result = new BackupVerificationResult();

            try
            {
                // Check if file exists
                if (!File.Exists(backupFile))
                {
                    result.IsValid = false;
                    result.Errors.Add("ملف النسخة الاحتياطية غير موجود - Backup file not found");
                    return result;
                }

                // Check file size
                var fileInfo = new FileInfo(backupFile);
                result.FileSizeBytes = fileInfo.Length;
                result.FileSizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2);

                if (fileInfo.Length == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("ملف النسخة الاحتياطية فارغ - Backup file is empty");
                    return result;
                }

                // Read file content
                string sqlContent = File.ReadAllText(backupFile, Encoding.UTF8);

                // Check for valid header
                if (!sqlContent.Contains("ZKTeco Manager Database Backup"))
                {
                    result.IsValid = false;
                    result.Errors.Add("صيغة ملف النسخة الاحتياطية غير صحيحة - Invalid backup file format");
                    return result;
                }

                result.HasValidHeader = true;

                // Extract metadata
                var versionMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"SCHEMA_VERSION:\s*(.+)");
                var dateMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"CREATED_AT:\s*(.+)");
                var machineMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"MACHINE_NAME:\s*(.+)");
                var recordsMatch = System.Text.RegularExpressions.Regex.Match(sqlContent, @"BACKUP COMPLETE:\s*(\d+)");

                if (versionMatch.Success) result.SchemaVersion = versionMatch.Groups[1].Value.Trim();
                if (dateMatch.Success)
                {
                    result.CreatedAt = dateMatch.Groups[1].Value.Trim();
                    if (DateTime.TryParse(result.CreatedAt, out DateTime createdDate))
                    {
                        result.CreatedDate = createdDate;
                    }
                }
                if (machineMatch.Success) result.MachineName = machineMatch.Groups[1].Value.Trim();
                if (recordsMatch.Success)
                {
                    if (int.TryParse(recordsMatch.Groups[1].Value.Trim(), out int totalRecords))
                    {
                        result.DeclaredRecordCount = totalRecords;
                    }
                }

                // Parse and count INSERT statements by table
                var tableInserts = ParseInsertStatements(sqlContent);
                result.TableCount = tableInserts.Count;
                result.ActualRecordCount = 0;

                foreach (var table in tableInserts)
                {
                    result.TableRecordCounts[table.Key] = table.Value.Count;
                    result.ActualRecordCount += table.Value.Count;
                }

                // Verify record count matches declared count
                if (result.DeclaredRecordCount > 0 && result.ActualRecordCount != result.DeclaredRecordCount)
                {
                    result.Warnings.Add($"عدد السجلات المعلن ({result.DeclaredRecordCount}) لا يتطابق مع العدد الفعلي ({result.ActualRecordCount})");
                }

                // Check for essential tables
                var essentialTables = new[] { "users", "departments" };
                foreach (var table in essentialTables)
                {
                    if (!tableInserts.ContainsKey(table) || tableInserts[table].Count == 0)
                    {
                        result.Warnings.Add($"جدول {table} فارغ أو غير موجود - Table {table} is empty or missing");
                    }
                }

                // Check schema version compatibility
                if (!string.IsNullOrEmpty(result.SchemaVersion) && result.SchemaVersion != SCHEMA_VERSION)
                {
                    result.Warnings.Add($"إصدار المخطط ({result.SchemaVersion}) مختلف عن الإصدار الحالي ({SCHEMA_VERSION})");
                }

                // Validate SQL syntax (basic check)
                var insertRegex = new System.Text.RegularExpressions.Regex(
                    @"INSERT INTO ""(\w+)""\s*\([^)]+\)\s*VALUES\s*\([^;]+\);",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var matches = insertRegex.Matches(sqlContent);

                if (matches.Count == 0 && result.DeclaredRecordCount > 0)
                {
                    result.Errors.Add("لم يتم العثور على أي عبارات INSERT صالحة - No valid INSERT statements found");
                    result.IsValid = false;
                    return result;
                }

                result.HasValidSqlSyntax = true;
                result.IsValid = result.Errors.Count == 0;

            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"خطأ أثناء التحقق: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Formats a value for safe SQL insertion.
        /// </summary>
        private static string FormatValueForSql(object rawValue)
        {
            if (rawValue == null || rawValue == DBNull.Value)
                return "NULL";

            if (rawValue is DateTime dt)
            {
                return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
            }
            else if (rawValue is TimeSpan ts)
            {
                return $"'{ts:hh\\:mm\\:ss}'";
            }
            else if (rawValue is bool b)
            {
                return b ? "true" : "false";
            }
            else if (rawValue is byte[] bytes)
            {
                return $"'\\x{BitConverter.ToString(bytes).Replace("-", "")}'";
            }
            else if (rawValue is int || rawValue is long || rawValue is short ||
                     rawValue is decimal || rawValue is double || rawValue is float)
            {
                return rawValue.ToString();
            }
            else
            {
                // Escape single quotes for string values
                string strValue = rawValue.ToString().Replace("'", "''");
                return $"'{strValue}'";
            }
        }
    }

    /// <summary>
    /// Result of backup verification.
    /// </summary>
    public class BackupVerificationResult
    {
        public bool IsValid { get; set; }
        public bool HasValidHeader { get; set; }
        public bool HasValidSqlSyntax { get; set; }
        public string SchemaVersion { get; set; }
        public string CreatedAt { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string MachineName { get; set; }
        public int DeclaredRecordCount { get; set; }
        public int ActualRecordCount { get; set; }
        public int TableCount { get; set; }
        public long FileSizeBytes { get; set; }
        public double FileSizeMB { get; set; }
        public Dictionary<string, int> TableRecordCounts { get; set; } = new Dictionary<string, int>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine(IsValid ? "✅ النسخة الاحتياطية صالحة" : "❌ النسخة الاحتياطية غير صالحة");
            sb.AppendLine();
            sb.AppendLine($"📅 تاريخ الإنشاء: {CreatedAt ?? "غير معروف"}");
            sb.AppendLine($"💻 جهاز المصدر: {MachineName ?? "غير معروف"}");
            sb.AppendLine($"📋 إصدار المخطط: {SchemaVersion ?? "غير معروف"}");
            sb.AppendLine($"📦 حجم الملف: {FileSizeMB} MB");
            sb.AppendLine();
            sb.AppendLine($"📊 عدد الجداول: {TableCount}");
            sb.AppendLine($"📝 عدد السجلات: {ActualRecordCount}");

            if (TableRecordCounts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("تفاصيل الجداول:");
                foreach (var table in TableRecordCounts)
                {
                    sb.AppendLine($"  • {table.Key}: {table.Value} سجل");
                }
            }

            if (Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("⚠️ تحذيرات:");
                foreach (var warning in Warnings)
                {
                    sb.AppendLine($"  • {warning}");
                }
            }

            if (Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("❌ أخطاء:");
                foreach (var error in Errors)
                {
                    sb.AppendLine($"  • {error}");
                }
            }

            return sb.ToString();
        }
    }
}
