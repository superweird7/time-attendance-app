using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using zkemkeeper;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// Service that automatically downloads attendance logs from devices at scheduled intervals.
    /// </summary>
    public static class AutoDownloadService
    {
        private static Timer _timer;
        private static bool _isRunning = false;
        private static readonly object _lock = new object();

        public static event EventHandler<AutoDownloadEventArgs> DownloadCompleted;

        public static bool IsEnabled { get; private set; }
        public static int IntervalMinutes { get; private set; } = 60;
        public static DateTime? LastDownload { get; private set; }

        /// <summary>
        /// Starts the auto-download service.
        /// </summary>
        public static void Start()
        {
            LoadSettings();

            if (!IsEnabled)
            {
                System.Diagnostics.Debug.WriteLine("Auto-download is disabled.");
                return;
            }

            if (_timer != null)
            {
                _timer.Dispose();
            }

            // Start timer with the configured interval
            int intervalMs = IntervalMinutes * 60 * 1000;
            _timer = new Timer(async _ => await ExecuteDownload(), null, intervalMs, intervalMs);

            System.Diagnostics.Debug.WriteLine($"Auto-download service started. Interval: {IntervalMinutes} minutes");
        }

        /// <summary>
        /// Stops the auto-download service.
        /// </summary>
        public static void Stop()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            System.Diagnostics.Debug.WriteLine("Auto-download service stopped.");
        }

        /// <summary>
        /// Loads settings from the database.
        /// </summary>
        public static void LoadSettings()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT enabled, interval_minutes, last_download FROM auto_download_settings LIMIT 1";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            IsEnabled = reader.GetBoolean(0);
                            IntervalMinutes = reader.GetInt32(1);
                            LastDownload = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading auto-download settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves settings to the database.
        /// </summary>
        public static void SaveSettings(bool enabled, int intervalMinutes)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    // Use UPSERT to ensure the row is created if it doesn't exist
                    var sql = @"INSERT INTO auto_download_settings (setting_id, enabled, interval_minutes)
                               VALUES (1, @enabled, @interval)
                               ON CONFLICT (setting_id) DO UPDATE SET enabled = @enabled, interval_minutes = @interval";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("enabled", enabled);
                        cmd.Parameters.AddWithValue("interval", intervalMinutes);
                        cmd.ExecuteNonQuery();
                    }
                }

                IsEnabled = enabled;
                IntervalMinutes = intervalMinutes;

                // Restart the service with new settings
                Stop();
                Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving auto-download settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes the download from all devices.
        /// </summary>
        private static async Task ExecuteDownload()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Auto-download started at {DateTime.Now}");

                var machines = GetAllMachines();
                int totalLogsDownloaded = 0;
                int successfulDevices = 0;

                foreach (var machine in machines)
                {
                    try
                    {
                        int logsDownloaded = await DownloadLogsFromDevice(machine);
                        if (logsDownloaded >= 0)
                        {
                            totalLogsDownloaded += logsDownloaded;
                            successfulDevices++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error downloading from {machine.MachineAlias}: {ex.Message}");
                    }
                }

                // Update last download time
                UpdateLastDownload();

                // Raise event
                DownloadCompleted?.Invoke(null, new AutoDownloadEventArgs
                {
                    TotalDevices = machines.Count,
                    SuccessfulDevices = successfulDevices,
                    TotalLogsDownloaded = totalLogsDownloaded,
                    CompletedAt = DateTime.Now
                });

                System.Diagnostics.Debug.WriteLine($"Auto-download completed. {totalLogsDownloaded} logs from {successfulDevices}/{machines.Count} devices.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-download error: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                }
            }
        }

        private static List<Machine> GetAllMachines()
        {
            var machines = new List<Machine>();
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT id, machine_alias, ip_address FROM machines";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            machines.Add(new Machine
                            {
                                Id = reader.GetInt32(0),
                                MachineAlias = reader.GetString(1),
                                IpAddress = reader.IsDBNull(2) ? "" : reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting machines: {ex.Message}");
            }
            return machines;
        }

        private static async Task<int> DownloadLogsFromDevice(Machine machine)
        {
            return await Task.Run(() =>
            {
                var zk = new CZKEM();
                int logsDownloaded = 0;
                int defaultPort = 4370;

                if (!zk.Connect_Net(machine.IpAddress, defaultPort))
                {
                    return -1; // Connection failed
                }

                try
                {
                    int machineNumber = 1;
                    zk.EnableDevice(machineNumber, false);

                    string dwEnrollNumber = "";
                    int dwVerifyMode = 0;
                    int dwInOutMode = 0;
                    int dwYear = 0, dwMonth = 0, dwDay = 0, dwHour = 0, dwMinute = 0, dwSecond = 0;
                    int dwWorkCode = 0;

                    if (zk.ReadGeneralLogData(machineNumber))
                    {
                        using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                        {
                            conn.Open();
                            while (zk.SSR_GetGeneralLogData(machineNumber, out dwEnrollNumber, out dwVerifyMode,
                                out dwInOutMode, out dwYear, out dwMonth, out dwDay, out dwHour, out dwMinute, out dwSecond, ref dwWorkCode))
                            {
                                var logTime = new DateTime(dwYear, dwMonth, dwDay, dwHour, dwMinute, dwSecond);

                                // Insert log (ignore duplicates)
                                var sql = @"INSERT INTO attendance_logs (user_badge_number, log_time, machine_id, verify_type)
                                           VALUES (@badge, @time, @machineId, @verifyType)
                                           ON CONFLICT (user_badge_number, log_time, machine_id) DO NOTHING";
                                using (var cmd = new NpgsqlCommand(sql, conn))
                                {
                                    cmd.Parameters.AddWithValue("badge", dwEnrollNumber);
                                    cmd.Parameters.AddWithValue("time", logTime);
                                    cmd.Parameters.AddWithValue("machineId", machine.Id);
                                    cmd.Parameters.AddWithValue("verifyType", dwVerifyMode);
                                    if (cmd.ExecuteNonQuery() > 0)
                                    {
                                        logsDownloaded++;
                                    }
                                }
                            }
                        }
                    }

                    zk.EnableDevice(machineNumber, true);
                }
                finally
                {
                    zk.Disconnect();
                }

                return logsDownloaded;
            });
        }

        private static void UpdateLastDownload()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "UPDATE auto_download_settings SET last_download = @time WHERE setting_id = 1";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("time", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
                LastDownload = DateTime.Now;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating last download time: {ex.Message}");
            }
        }

        /// <summary>
        /// Manually triggers a download.
        /// </summary>
        public static async Task TriggerDownloadAsync()
        {
            await ExecuteDownload();
        }
    }

    public class AutoDownloadEventArgs : EventArgs
    {
        public int TotalDevices { get; set; }
        public int SuccessfulDevices { get; set; }
        public int TotalLogsDownloaded { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
