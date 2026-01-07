using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using zkemkeeper;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class SystemHealthWindow : Window
    {
        public SystemHealthWindow()
        {
            InitializeComponent();
            this.Loaded += SystemHealthWindow_Loaded;
        }

        private async void SystemHealthWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshHealthInfoAsync();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshHealthInfoAsync();
        }

        private async Task RefreshHealthInfoAsync()
        {
            // Show loading states immediately
            DatabaseStatusText.Text = "جاري الفحص...";
            DatabaseStatusBadgeText.Text = "...";
            TotalDevicesText.Text = "-";
            OnlineDevicesText.Text = "-";
            OfflineDevicesText.Text = "-";
            DeviceStatusBadgeText.Text = "جاري الفحص...";
            LastBackupText.Text = "جاري الفحص...";
            BackupStatusBadgeText.Text = "...";
            LastLogDownloadText.Text = "جاري الفحص...";
            LogDownloadStatusBadgeText.Text = "...";

            // Run quick checks first (these are fast)
            CheckDatabaseConnection();
            CheckLastBackup();
            CheckLastLogDownload();
            CheckDiskSpace();
            CheckSystemInfo();
            LastCheckTimeText.Text = $"آخر فحص: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            // Run device check in background (this is slow)
            await CheckDeviceStatusAsync();
        }

        private void CheckDatabaseConnection()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT version()", conn))
                    {
                        var version = cmd.ExecuteScalar()?.ToString() ?? "غير معروف";
                        // Extract just the main version info
                        if (version.Contains(","))
                            version = version.Substring(0, version.IndexOf(","));

                        DatabaseStatusText.Text = version;
                        DatabaseStatusBadgeText.Text = "متصل";
                        DatabaseStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5"));
                        DatabaseStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseStatusText.Text = $"خطأ: {ex.Message}";
                DatabaseStatusBadgeText.Text = "غير متصل";
                DatabaseStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                DatabaseStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            }
        }

        private async Task CheckDeviceStatusAsync()
        {
            try
            {
                // Get device IPs from database first
                var deviceIps = new List<string>();
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT ip_address FROM machines", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            deviceIps.Add(reader.GetString(0));
                        }
                    }
                }

                int totalDevices = deviceIps.Count;
                TotalDevicesText.Text = totalDevices.ToString();

                if (totalDevices == 0)
                {
                    OnlineDevicesText.Text = "0";
                    OfflineDevicesText.Text = "0";
                    DeviceStatusBadgeText.Text = "لا يوجد";
                    DeviceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                    DeviceStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                    return;
                }

                // Check devices in parallel in background
                int onlineDevices = 0;
                var result = await Task.Run(() =>
                {
                    int online = 0;
                    foreach (var ip in deviceIps)
                    {
                        try
                        {
                            var zk = new CZKEM();
                            // Set a shorter timeout by trying to connect quickly
                            if (zk.Connect_Net(ip, 4370))
                            {
                                online++;
                                zk.Disconnect();
                            }
                        }
                        catch
                        {
                            // Device offline or error
                        }
                    }
                    return online;
                });

                onlineDevices = result;
                int offlineDevices = totalDevices - onlineDevices;

                // Update UI on main thread
                OnlineDevicesText.Text = onlineDevices.ToString();
                OfflineDevicesText.Text = offlineDevices.ToString();

                if (offlineDevices == 0)
                {
                    DeviceStatusBadgeText.Text = "ممتاز";
                    DeviceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5"));
                    DeviceStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
                else if (onlineDevices > offlineDevices)
                {
                    DeviceStatusBadgeText.Text = "جيد";
                    DeviceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
                    DeviceStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                }
                else
                {
                    DeviceStatusBadgeText.Text = "تحذير";
                    DeviceStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    DeviceStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                }
            }
            catch (Exception ex)
            {
                TotalDevicesText.Text = "0";
                OnlineDevicesText.Text = "0";
                OfflineDevicesText.Text = "0";
                DeviceStatusBadgeText.Text = "خطأ";
                System.Diagnostics.Debug.WriteLine($"Error checking device status: {ex.Message}");
            }
        }

        private void CheckLastBackup()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT last_backup_date FROM backup_settings LIMIT 1", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            var lastBackup = (DateTime)result;
                            var diff = DateTime.Now - lastBackup;
                            LastBackupText.Text = $"{lastBackup:yyyy-MM-dd HH:mm}";

                            if (diff.TotalDays < 1)
                            {
                                BackupStatusBadgeText.Text = "حديث";
                                BackupStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5"));
                                BackupStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                            }
                            else if (diff.TotalDays < 7)
                            {
                                BackupStatusBadgeText.Text = $"منذ {(int)diff.TotalDays} يوم";
                                BackupStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
                                BackupStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                            }
                            else
                            {
                                BackupStatusBadgeText.Text = "قديم";
                                BackupStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                                BackupStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                            }
                        }
                        else
                        {
                            LastBackupText.Text = "لم يتم النسخ بعد";
                            BackupStatusBadgeText.Text = "لا يوجد";
                            BackupStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                            BackupStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LastBackupText.Text = "غير متاح";
                BackupStatusBadgeText.Text = "خطأ";
                System.Diagnostics.Debug.WriteLine($"Error checking backup: {ex.Message}");
            }
        }

        private void CheckLastLogDownload()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    // Check last_download from download_schedule_settings
                    using (var cmd = new NpgsqlCommand("SELECT last_download FROM download_schedule_settings LIMIT 1", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            var lastDownload = (DateTime)result;
                            var diff = DateTime.Now - lastDownload;
                            LastLogDownloadText.Text = $"{lastDownload:yyyy-MM-dd HH:mm}";

                            if (diff.TotalHours < 24)
                            {
                                LogDownloadStatusBadgeText.Text = "حديث";
                                LogDownloadStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5"));
                                LogDownloadStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                            }
                            else if (diff.TotalDays < 3)
                            {
                                LogDownloadStatusBadgeText.Text = $"منذ {(int)diff.TotalDays} يوم";
                                LogDownloadStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
                                LogDownloadStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                            }
                            else
                            {
                                LogDownloadStatusBadgeText.Text = "قديم";
                                LogDownloadStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                                LogDownloadStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                            }
                        }
                        else
                        {
                            LastLogDownloadText.Text = "لم يتم التحميل بعد";
                            LogDownloadStatusBadgeText.Text = "لا يوجد";
                        }
                    }
                }
            }
            catch
            {
                // Table might not exist, try attendance_logs
                try
                {
                    using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();
                        using (var cmd = new NpgsqlCommand("SELECT MAX(created_at) FROM attendance_logs", conn))
                        {
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                var lastLog = (DateTime)result;
                                LastLogDownloadText.Text = $"آخر سجل: {lastLog:yyyy-MM-dd HH:mm}";
                                LogDownloadStatusBadgeText.Text = "متاح";
                            }
                            else
                            {
                                LastLogDownloadText.Text = "لا توجد سجلات";
                                LogDownloadStatusBadgeText.Text = "لا يوجد";
                            }
                        }
                    }
                }
                catch
                {
                    LastLogDownloadText.Text = "غير متاح";
                    LogDownloadStatusBadgeText.Text = "خطأ";
                }
            }
        }

        private void CheckDiskSpace()
        {
            try
            {
                string driveLetter = Path.GetPathRoot(Environment.CurrentDirectory);
                DriveInfo drive = new DriveInfo(driveLetter);

                long totalGB = drive.TotalSize / (1024 * 1024 * 1024);
                long freeGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                long usedGB = totalGB - freeGB;
                int usedPercent = (int)((usedGB * 100) / totalGB);

                DiskSpaceText.Text = $"محرك {driveLetter} - المتاح: {freeGB} GB من {totalGB} GB";
                DiskSpaceProgress.Value = usedPercent;

                if (usedPercent > 90)
                {
                    DiskStatusBadgeText.Text = "ممتلئ";
                    DiskStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    DiskStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    DiskSpaceProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                }
                else if (usedPercent > 75)
                {
                    DiskStatusBadgeText.Text = "تحذير";
                    DiskStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
                    DiskStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                    DiskSpaceProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                }
                else
                {
                    DiskStatusBadgeText.Text = $"{100 - usedPercent}% متاح";
                    DiskStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5"));
                    DiskStatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    DiskSpaceProgress.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                }
            }
            catch (Exception ex)
            {
                DiskSpaceText.Text = "غير متاح";
                DiskStatusBadgeText.Text = "خطأ";
                System.Diagnostics.Debug.WriteLine($"Error checking disk space: {ex.Message}");
            }
        }

        private void CheckSystemInfo()
        {
            try
            {
                string osVersion = Environment.OSVersion.ToString();
                string machineName = Environment.MachineName;
                int processorCount = Environment.ProcessorCount;
                long workingSet = Environment.WorkingSet / (1024 * 1024); // MB

                SystemInfoText.Text = $"الجهاز: {machineName} | النظام: {osVersion} | المعالجات: {processorCount} | الذاكرة المستخدمة: {workingSet} MB";
            }
            catch (Exception ex)
            {
                SystemInfoText.Text = "غير متاح";
                System.Diagnostics.Debug.WriteLine($"Error getting system info: {ex.Message}");
            }
        }
    }
}
