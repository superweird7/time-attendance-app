using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using zkemkeeper;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Services;

namespace ZKTecoManager
{
    public partial class DevicesWindow : Window
    {
        private List<Machine> allMachines = new List<Machine>();

        public DevicesWindow()
        {
            InitializeComponent();
            this.Loaded += DevicesWindow_Loaded;
        }

        private async void DevicesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMachineList();
            LoadLocations();
            RefreshAttendanceLogList();
            await RefreshDeviceStatuses();
            UpdateHealthSummary();
            LoadAutoDownloadSettings();
        }

        private void LoadLocations()
        {
            try
            {
                var locations = new List<string>();
                locations.Add("الكل"); // "All" option

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT DISTINCT location FROM machines WHERE location IS NOT NULL AND location <> '' ORDER BY location";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            locations.Add(reader.GetString(0));
                        }
                    }
                }

                LocationFilterComboBox.ItemsSource = locations;
                LocationFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading locations: {ex.Message}");
            }
        }

        private void LocationFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || allMachines == null) return;

            string selectedLocation = LocationFilterComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedLocation) || selectedLocation == "الكل")
            {
                // Show all machines
                MachineListBox.ItemsSource = allMachines;
            }
            else
            {
                // Filter by location
                var filtered = allMachines.Where(m => m.Location == selectedLocation).ToList();
                MachineListBox.ItemsSource = filtered;
            }

            UpdateHealthSummary();
        }

        private void LoadAutoDownloadSettings()
        {
            AutoDownloadService.LoadSettings();
            AutoDownloadEnabledCheckBox.IsChecked = AutoDownloadService.IsEnabled;

            // Set interval combobox
            foreach (ComboBoxItem item in AutoDownloadIntervalComboBox.Items)
            {
                if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tag) && tag == AutoDownloadService.IntervalMinutes)
                {
                    AutoDownloadIntervalComboBox.SelectedItem = item;
                    break;
                }
            }

            // Update last download text
            if (AutoDownloadService.LastDownload.HasValue)
            {
                LastAutoDownloadText.Text = AutoDownloadService.LastDownload.Value.ToString("yyyy-MM-dd HH:mm");
            }
            else
            {
                LastAutoDownloadText.Text = "لم يتم بعد";
            }
        }

        private void AutoDownloadEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            bool enabled = AutoDownloadEnabledCheckBox.IsChecked == true;
            int interval = GetSelectedInterval();

            AutoDownloadService.SaveSettings(enabled, interval);

            MessageBox.Show(enabled
                ? $"تم تفعيل التحميل التلقائي كل {interval} دقيقة"
                : "تم إيقاف التحميل التلقائي",
                "إعدادات التحميل التلقائي", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AutoDownloadIntervalComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            bool enabled = AutoDownloadEnabledCheckBox.IsChecked == true;
            int interval = GetSelectedInterval();

            if (enabled)
            {
                AutoDownloadService.SaveSettings(enabled, interval);
            }
        }

        private int GetSelectedInterval()
        {
            if (AutoDownloadIntervalComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (int.TryParse(item.Tag.ToString(), out int interval))
                {
                    return interval;
                }
            }
            return 60; // default
        }

        #region Window Behavior
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        #region Status Check Logic
        private async void RefreshStatuses_Click(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            await RefreshDeviceStatuses();
            UpdateHealthSummary();
            this.Cursor = Cursors.Arrow;
            MessageBox.Show("تم تحديث حالات الأجهزة بنجاح!", "تم التحديث", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateHealthSummary()
        {
            // Get the currently displayed machines (could be filtered)
            var displayedMachines = MachineListBox.ItemsSource as List<Machine> ?? allMachines;

            int total = displayedMachines.Count;
            int online = displayedMachines.Count(m => m.Status == "Online");
            int offline = total - online;

            TotalDevicesText.Text = total.ToString();
            OnlineDevicesText.Text = online.ToString();
            OfflineDevicesText.Text = offline.ToString();
            LastCheckTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private async Task RefreshDeviceStatuses()
        {
            foreach (var machine in allMachines)
            {
                machine.Status = "جاري الفحص...";
                await Task.Run(() =>
                {
                    var zk = new CZKEM();
                    bool isConnected = zk.Connect_Net(machine.IpAddress, 4370);

                    if (isConnected)
                    {
                        int machineNumber = 1;
                        int userCount = 0;
                        int fpCount = 0;

                        zk.EnableDevice(machineNumber, false);
                        zk.GetDeviceStatus(machineNumber, 2, ref userCount);
                        zk.GetDeviceStatus(machineNumber, 3, ref fpCount);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            machine.Status = "Online";
                            machine.UserCount = userCount;
                            machine.FingerprintCount = fpCount;
                        });

                        zk.EnableDevice(machineNumber, true);
                        zk.Disconnect();
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            machine.Status = "Offline";
                            machine.UserCount = 0;
                            machine.FingerprintCount = 0;
                        });
                    }
                });
            }
        }
        #endregion

        #region Machine Management
        private async void AddMachine_Click(object sender, RoutedEventArgs e)
        {
            var addMachineWindow = new AddMachineWindow { Owner = this };
            if (addMachineWindow.ShowDialog() == true)
            {
                RefreshMachineList();
                await RefreshDeviceStatuses();
            }
        }

        private async void EditMachine_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                var editMachineWindow = new EditMachineWindow(selectedMachine) { Owner = this };
                if (editMachineWindow.ShowDialog() == true)
                {
                    RefreshMachineList();
                    await RefreshDeviceStatuses();
                }
            }
            else
            {
                MessageBox.Show("الرجاء اختيار جهاز للتعديل", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteMachine_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                if (MessageBox.Show($"هل أنت متأكد من حذف الجهاز '{selectedMachine.MachineAlias}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                        {
                            connection.Open();
                            var sql = "DELETE FROM machines WHERE id = @id";
                            using (var cmd = new NpgsqlCommand(sql, connection))
                            {
                                cmd.Parameters.AddWithValue("id", selectedMachine.Id);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        // Log the deletion
                        AuditLogger.Log("DELETE", "machines", selectedMachine.Id,
                            $"الاسم: {selectedMachine.MachineAlias}, IP: {selectedMachine.IpAddress}",
                            null,
                            $"حذف جهاز: {selectedMachine.MachineAlias} ({selectedMachine.IpAddress})");

                        RefreshMachineList();
                        MessageBox.Show("تم حذف الجهاز بنجاح!", "نجح", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"فشل حذف الجهاز:\n\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("الرجاء اختيار جهاز للحذف", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region Device Interaction
        private void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                var zk = new CZKEM();
                this.Cursor = Cursors.Wait;

                try
                {
                    if (zk.Connect_Net(selectedMachine.IpAddress, 4370))
                    {
                        MessageBox.Show($"تم الاتصال بنجاح مع '{selectedMachine.MachineAlias}'!", "نجح الاتصال", MessageBoxButton.OK, MessageBoxImage.Information);
                        zk.Disconnect();
                    }
                    else
                    {
                        int errorCode = 0;
                        zk.GetLastError(ref errorCode);
                        MessageBox.Show($"فشل الاتصال مع '{selectedMachine.MachineAlias}'.\n\nرمز الخطأ: {errorCode}", "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
            else
            {
                MessageBox.Show("الرجاء اختيار جهاز من القائمة للاختبار", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SyncUsers_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                var syncMenu = new SyncMenuWindow(selectedMachine) { Owner = this };
                syncMenu.ShowDialog();
            }
            else
            {
                MessageBox.Show("الرجاء اختيار جهاز للمزامنة معه", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeviceSettings_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                var settingsWindow = new DeviceSettingsWindow(selectedMachine) { Owner = this };
                settingsWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("الرجاء اختيار جهاز لعرض إعداداته", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CopyUsers_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                if (allMachines.Count < 2)
                {
                    MessageBox.Show("يجب أن يكون لديك جهازين على الأقل لنسخ المستخدمين بينهما",
                        "لا توجد أجهزة كافية", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var copyWindow = new CopyUsersWindow(selectedMachine, allMachines) { Owner = this };
                copyWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("الرجاء اختيار جهاز المصدر (الجهاز الذي تريد النسخ منه)",
                    "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DownloadLogs_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                LoadingOverlay.Show($"جاري تحميل السجلات من {selectedMachine.MachineAlias}...", "يرجى الانتظار");
                this.Cursor = Cursors.Wait;

                // Force UI to update and show LoadingOverlay before starting the operation
                await Task.Delay(50);

                int logsDownloaded = 0;
                var allBadgeNumbers = new List<string>();

                try
                {
                    var result = await Task.Run(() =>
                    {
                        var zk = new CZKEM();
                        if (!zk.Connect_Net(selectedMachine.IpAddress, 4370))
                        {
                            return new { Success = false, ErrorMessage = "تعذر الاتصال بالجهاز", LogsCount = 0, BadgeNumbers = new List<string>() };
                        }

                        var badgeNumbers = new List<string>();
                        int count = 0;

                        zk.EnableDevice(1, false);

                        try
                        {
                            // Read ALL logs (not just new ones)
                            if (zk.ReadAllGLogData(1))
                            {
                                string badgeNumber;
                                int verifyMode, inOutMode, year, month, day, hour, minute, second, workCode = 0;

                                while (zk.SSR_GetGeneralLogData(1, out badgeNumber, out verifyMode, out inOutMode,
                                       out year, out month, out day, out hour, out minute, out second, ref workCode))
                                {
                                    try
                                    {
                                        var logTime = new DateTime(year, month, day, hour, minute, second);

                                        System.Diagnostics.Debug.WriteLine($"Downloaded: {badgeNumber} - {logTime:yyyy-MM-dd HH:mm:ss}");

                                        SaveAttendanceLog(badgeNumber, logTime, selectedMachine.Id);
                                        badgeNumbers.Add(badgeNumber);
                                        count++;
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                                    }
                                }
                            }

                            return new { Success = true, ErrorMessage = "", LogsCount = count, BadgeNumbers = badgeNumbers };
                        }
                        finally
                        {
                            zk.EnableDevice(1, true);
                            zk.Disconnect();
                        }
                    });

                    if (!result.Success)
                    {
                        MessageBox.Show(result.ErrorMessage, "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    logsDownloaded = result.LogsCount;
                    allBadgeNumbers = result.BadgeNumbers;

                    RefreshAttendanceLogList();

                    // Audit log the download operation
                    AuditLogger.Log(
                        "DOWNLOAD",
                        "attendance_logs",
                        null,
                        null,
                        $"Device: {selectedMachine.MachineAlias}, Records: {logsDownloaded}",
                        $"Downloaded {logsDownloaded} attendance records from device {selectedMachine.MachineAlias}"
                    );

                    MessageBox.Show($"تم تحميل {logsDownloaded} سجل بنجاح!", "اكتمل", MessageBoxButton.OK, MessageBoxImage.Information);

                    var resultsWindow = new NewUsersWindow(logsDownloaded, allBadgeNumbers) { Owner = this };
                    resultsWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    this.Cursor = Cursors.Arrow;
                    LoadingOverlay.Hide();
                }
            }
            else
            {
                MessageBox.Show("الرجاء اختيار جهاز من القائمة", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        private async void DownloadAllLogs_Click(object sender, RoutedEventArgs e)
        {
            if (allMachines == null || allMachines.Count == 0)
            {
                MessageBox.Show("لا توجد أجهزة متاحة للتحميل منها", "لا توجد أجهزة", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"سيتم تحميل سجلات الحضور من جميع الأجهزة الـ {allMachines.Count}. قد يستغرق هذا بعض الوقت.\n\nهل تريد المتابعة؟",
                "تحميل جميع السجلات", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            LoadingOverlay.Show("جاري تحميل سجلات جميع الأجهزة...", "يرجى الانتظار");
            this.Cursor = Cursors.Wait;
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            int totalLogsDownloaded = 0;
            int onlineDevices = 0;
            int offlineDevices = 0;
            var allBadgeNumbers = new List<string>();
            var offlineDeviceNames = new List<string>();
            string originalTitle = this.Title;

            try
            {
                // ✅ PARALLEL DOWNLOAD: Process all devices simultaneously
                var downloadTasks = allMachines.Select(machine => Task.Run(() =>
                {
                    int logsCount = 0;
                    var badgeNumbers = new List<string>();
                    bool success = false;

                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.Title = $"جاري التحميل من {allMachines.Count} أجهزة...";
                        });

                        var zk = new CZKEM();

                        if (!zk.Connect_Net(machine.IpAddress, 4370))
                        {
                            return new { Machine = machine, Success = false, LogsCount = 0, BadgeNumbers = badgeNumbers };
                        }

                        zk.EnableDevice(1, false);

                        try
                        {
                            if (zk.ReadGeneralLogData(1))
                            {
                                string badgeNumber;
                                int verifyMode, inOutMode, year, month, day, hour, minute, second, workCode = 0;

                                while (zk.SSR_GetGeneralLogData(1, out badgeNumber, out verifyMode, out inOutMode,
                                       out year, out month, out day, out hour, out minute, out second, ref workCode))
                                {
                                    var logTime = new DateTime(year, month, day, hour, minute, second);
                                    SaveAttendanceLog(badgeNumber, logTime, machine.Id);
                                    badgeNumbers.Add(badgeNumber);
                                    logsCount++;
                                }
                            }
                            success = true;
                        }
                        finally
                        {
                            zk.EnableDevice(1, true);
                            zk.Disconnect();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error downloading from {machine.MachineAlias}: {ex.Message}");
                    }

                    return new { Machine = machine, Success = success, LogsCount = logsCount, BadgeNumbers = badgeNumbers };
                })).ToList();

                // ✅ Wait for all downloads to complete
                var results = await Task.WhenAll(downloadTasks);

                // ✅ Process results
                foreach (var deviceResult in results)
                {
                    if (deviceResult.Success)
                    {
                        onlineDevices++;
                        totalLogsDownloaded += deviceResult.LogsCount;
                        allBadgeNumbers.AddRange(deviceResult.BadgeNumbers);
                    }
                    else
                    {
                        offlineDevices++;
                        offlineDeviceNames.Add(deviceResult.Machine.MachineAlias);
                    }
                }

                RefreshAttendanceLogList();

                // Audit log the bulk download operation
                AuditLogger.Log(
                    "DOWNLOAD",
                    "attendance_logs",
                    null,
                    null,
                    $"All Devices: {onlineDevices} online, Records: {totalLogsDownloaded}",
                    $"Downloaded {totalLogsDownloaded} attendance records from {onlineDevices} devices"
                );

                // Build summary message
                string summaryMessage = $"📊 ملخص التنزيل - Download Summary\n\n" +
                                       $"✅ الأجهزة المتصلة: {onlineDevices}\n" +
                                       $"   Online Devices: {onlineDevices}\n\n" +
                                       $"❌ الأجهزة غير المتصلة: {offlineDevices}\n" +
                                       $"   Offline Devices: {offlineDevices}\n";

                if (offlineDeviceNames.Count > 0)
                {
                    summaryMessage += "\n🔴 الأجهزة غير المتصلة - Offline Devices:\n";
                    foreach (var deviceName in offlineDeviceNames)
                    {
                        summaryMessage += $"   • {deviceName}\n";
                    }
                    summaryMessage += "\n";
                }

                summaryMessage += $"📥 إجمالي السجلات المنزلة: {totalLogsDownloaded}\n" +
                                 $"   Total Logs Downloaded: {totalLogsDownloaded}\n\n" +
                                 $"📱 إجمالي الأجهزة: {allMachines.Count}\n" +
                                 $"   Total Devices: {allMachines.Count}";

                MessageBox.Show(summaryMessage, "اكتمل التنزيل - Download Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                var resultsWindow = new NewUsersWindow(totalLogsDownloaded, allBadgeNumbers) { Owner = this };
                resultsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء التحميل:\n\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                this.Title = originalTitle;
                LoadingOverlay.Hide();
            }
        }

        private async void DownloadFingerprints_Click(object sender, RoutedEventArgs e)
        {
            if (MachineListBox.SelectedItem is Machine selectedMachine)
            {
                LoadingOverlay.Show($"جاري الاتصال بـ {selectedMachine.MachineAlias}...", "يرجى الانتظار");
                this.Cursor = Cursors.Wait;
                await Task.Delay(50);

                int fingerprintsDownloaded = 0;
                int usersProcessed = 0;
                int totalUsers = 0;

                try
                {
                    var zk = new CZKEM();
                    bool connected = await Task.Run(() => zk.Connect_Net(selectedMachine.IpAddress, 4370));

                    if (!connected)
                    {
                        MessageBox.Show("لا يمكن الاتصال بالجهاز", "فشل الاتصال",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        zk.EnableDevice(1, false);

                        // Step 1: Read all users first to get count
                        LoadingOverlay.Show("جاري قراءة قائمة المستخدمين...", selectedMachine.MachineAlias);
                        await Task.Delay(30);

                        var deviceUsers = new List<(string Badge, string Name)>();
                        await Task.Run(() =>
                        {
                            if (zk.ReadAllUserID(1))
                            {
                                string badge = "", name = "", password = "";
                                int privilege = 0;
                                bool enabled = false;

                                while (zk.SSR_GetAllUserInfo(1, out badge, out name, out password, out privilege, out enabled))
                                {
                                    deviceUsers.Add((badge, name));
                                }
                            }
                        });

                        totalUsers = deviceUsers.Count;

                        if (totalUsers == 0)
                        {
                            MessageBox.Show("لا يوجد مستخدمين في الجهاز", "لا يوجد بيانات",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        // Step 2: Batch read ALL templates at once (much faster than individual reads)
                        LoadingOverlay.Show($"جاري تحميل البصمات ({totalUsers} مستخدم)...", "قراءة جميع القوالب دفعة واحدة");
                        await Task.Delay(30);

                        await Task.Run(() => zk.ReadAllTemplate(1));

                        // Step 3: Process each user's fingerprints from cached data
                        int currentUser = 0;
                        foreach (var user in deviceUsers)
                        {
                            currentUser++;

                            // Update progress every 10 users
                            if (currentUser % 10 == 0 || currentUser == totalUsers)
                            {
                                int progress = (currentUser * 100) / totalUsers;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LoadingOverlay.Show($"جاري معالجة المستخدمين ({currentUser}/{totalUsers})...", $"{progress}%");
                                });
                            }

                            // Get user ID from database
                            int? userId = GetUserIdByBadge(user.Badge.TrimStart('0'));

                            if (userId.HasValue)
                            {
                                usersProcessed++;

                                // Get fingerprints from cached data (fast - no device call needed)
                                await Task.Run(() =>
                                {
                                    for (int fingerIndex = 0; fingerIndex <= 9; fingerIndex++)
                                    {
                                        string fingerTemplate = "";
                                        int templateLength = 0;
                                        int flag = 0;

                                        if (zk.GetUserTmpExStr(1, user.Badge, fingerIndex, out flag, out fingerTemplate, out templateLength))
                                        {
                                            if (!string.IsNullOrEmpty(fingerTemplate) && templateLength > 0)
                                            {
                                                SaveBiometricDataToDb(userId.Value, fingerIndex, fingerTemplate);
                                                fingerprintsDownloaded++;
                                            }
                                        }
                                    }
                                });
                            }
                        }
                    }
                    finally
                    {
                        await Task.Run(() =>
                        {
                            zk.EnableDevice(1, true);
                            zk.Disconnect();
                        });
                    }

                    // Log the action
                    AuditLogger.Log("SYNC", "biometric_data", null, null, null,
                        $"تحميل {fingerprintsDownloaded} بصمة من جهاز {selectedMachine.MachineAlias} لـ {usersProcessed} مستخدم");

                    // Ask user if they want to export
                    var exportResult = MessageBox.Show($"تم تحميل البصمات بنجاح!\n\n" +
                        $"إجمالي المستخدمين في الجهاز: {totalUsers}\n" +
                        $"المستخدمين المعالجين (الموجودين في قاعدة البيانات): {usersProcessed}\n" +
                        $"البصمات المنزلة: {fingerprintsDownloaded}\n\n" +
                        $"هل تريد تصدير البصمات إلى ملف؟",
                        "اكتمل التنزيل", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (exportResult == MessageBoxResult.Yes)
                    {
                        ExportFingerprintsToFile(selectedMachine.MachineAlias);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء تحميل البصمات:\n\n{ex.Message}",
                        "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    this.Cursor = Cursors.Arrow;
                    LoadingOverlay.Hide();
                }
            }
            else
            {
                MessageBox.Show("الرجاء تحديد جهاز من القائمة أولاً", "لا يوجد تحديد",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private int? GetUserIdByBadge(string badgeNumber)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT user_id FROM users WHERE badge_number = @badge OR badge_number = @badgePadded";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("badge", badgeNumber);
                        cmd.Parameters.AddWithValue("badgePadded", badgeNumber.PadLeft(10, '0'));
                        var result = cmd.ExecuteScalar();
                        return result != null ? (int?)Convert.ToInt32(result) : null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void ExportFingerprintsToFile(string deviceName)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "تصدير البصمات",
                    Filter = "ملف البصمات (*.fp)|*.fp|جميع الملفات (*.*)|*.*",
                    FileName = $"Fingerprints_{deviceName}_{DateTime.Now:yyyyMMdd_HHmmss}.fp",
                    DefaultExt = ".fp"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    LoadingOverlay.Show("جاري تصدير البصمات...", "يرجى الانتظار");

                    int exportedCount = 0;
                    using (var writer = new System.IO.StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Write header
                        writer.WriteLine("# ZKTeco Fingerprint Export");
                        writer.WriteLine($"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"# Device: {deviceName}");
                        writer.WriteLine("# Format: BadgeNumber|FingerIndex|TemplateData");
                        writer.WriteLine();

                        using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                        {
                            conn.Open();
                            var sql = @"SELECT u.badge_number, b.finger_index, b.template_data
                                       FROM biometric_data b
                                       INNER JOIN users u ON b.user_id_fk = u.user_id
                                       WHERE b.template_data IS NOT NULL AND b.template_data <> ''
                                       ORDER BY u.badge_number, b.finger_index";

                            using (var cmd = new NpgsqlCommand(sql, conn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string badge = reader.GetString(0);
                                    int fingerIndex = reader.GetInt32(1);
                                    string template = reader.GetString(2);

                                    writer.WriteLine($"{badge}|{fingerIndex}|{template}");
                                    exportedCount++;
                                }
                            }
                        }
                    }

                    LoadingOverlay.Hide();

                    MessageBox.Show($"تم تصدير {exportedCount} بصمة بنجاح!\n\nالمسار: {saveDialog.FileName}",
                        "اكتمل التصدير", MessageBoxButton.OK, MessageBoxImage.Information);

                    AuditLogger.Log("EXPORT", "biometric_data", null, null, null,
                        $"تصدير {exportedCount} بصمة إلى {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Hide();
                MessageBox.Show($"حدث خطأ أثناء التصدير:\n\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UploadFingerprintsFromFile_Click(object sender, RoutedEventArgs e)
        {
            if (!(MachineListBox.SelectedItem is Machine selectedMachine))
            {
                MessageBox.Show("الرجاء تحديد جهاز من القائمة أولاً", "لا يوجد تحديد",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "اختر ملف البصمات",
                Filter = "ملف البصمات (*.fp)|*.fp|جميع الملفات (*.*)|*.*",
                DefaultExt = ".fp"
            };

            if (openDialog.ShowDialog() != true)
                return;

            // Read and parse the file first
            var fingerprints = new List<(string Badge, int FingerIndex, string Template)>();
            try
            {
                var lines = System.IO.File.ReadAllLines(openDialog.FileName, System.Text.Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        string badge = parts[0];
                        if (int.TryParse(parts[1], out int fingerIndex))
                        {
                            string template = parts[2];
                            fingerprints.Add((badge, fingerIndex, template));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في قراءة الملف:\n\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (fingerprints.Count == 0)
            {
                MessageBox.Show("الملف لا يحتوي على بصمات صالحة", "ملف فارغ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"سيتم رفع {fingerprints.Count} بصمة إلى جهاز {selectedMachine.MachineAlias}\n\nهل تريد المتابعة؟",
                "تأكيد الرفع", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            LoadingOverlay.Show($"جاري الاتصال بـ {selectedMachine.MachineAlias}...", "يرجى الانتظار");
            this.Cursor = Cursors.Wait;
            await Task.Delay(50);

            int uploadedCount = 0;
            int failedCount = 0;

            try
            {
                var zk = new CZKEM();
                bool connected = await Task.Run(() => zk.Connect_Net(selectedMachine.IpAddress, 4370));

                if (!connected)
                {
                    MessageBox.Show("لا يمكن الاتصال بالجهاز", "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    zk.EnableDevice(1, false);

                    int total = fingerprints.Count;
                    int current = 0;

                    foreach (var fp in fingerprints)
                    {
                        current++;
                        if (current % 10 == 0 || current == total)
                        {
                            int progress = (current * 100) / total;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                LoadingOverlay.Show($"جاري رفع البصمات ({current}/{total})...", $"{progress}%");
                            });
                        }

                        bool success = await Task.Run(() =>
                        {
                            // Pad badge number to match device format
                            string paddedBadge = fp.Badge.PadLeft(10, '0');
                            return zk.SetUserTmpExStr(1, paddedBadge, fp.FingerIndex, 1, fp.Template);
                        });

                        if (success)
                            uploadedCount++;
                        else
                            failedCount++;
                    }
                }
                finally
                {
                    await Task.Run(() =>
                    {
                        zk.RefreshData(1);
                        zk.EnableDevice(1, true);
                        zk.Disconnect();
                    });
                }

                AuditLogger.Log("SYNC", "biometric_data", null, null, null,
                    $"رفع {uploadedCount} بصمة إلى جهاز {selectedMachine.MachineAlias} من ملف");

                MessageBox.Show($"اكتمل رفع البصمات!\n\n" +
                    $"البصمات المرفوعة بنجاح: {uploadedCount}\n" +
                    $"البصمات الفاشلة: {failedCount}",
                    "اكتمل الرفع", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء رفع البصمات:\n\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
                LoadingOverlay.Hide();
            }
        }

        private void SaveBiometricDataToDb(int userId, int fingerIndex, string templateData)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"
                        INSERT INTO biometric_data (user_id_fk, finger_index, template_data, biometric_type)
                        VALUES (@userId, @fingerIndex, @template, 1)
                        ON CONFLICT (user_id_fk, finger_index, biometric_type)
                        DO UPDATE SET template_data = @template, updated_at = CURRENT_TIMESTAMP";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", userId);
                        cmd.Parameters.AddWithValue("fingerIndex", fingerIndex);
                        cmd.Parameters.AddWithValue("template", templateData);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving biometric data: {ex.Message}");
            }
        }

        #endregion

        #region Helper and Database Methods
        private void RefreshMachineList()
        {
            try
            {
                allMachines = GetMachinesFromDatabase();
                MachineListBox.ItemsSource = null;
                MachineListBox.ItemsSource = allMachines;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الأجهزة:\n\n{ex.Message}\n\nتتبع المكدس:\n{ex.StackTrace}",
                    "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshAttendanceLogList()
        {
            try
            {
                AttendanceLogListBox.ItemsSource = GetAttendanceLogsFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل سجلات الحضور:\n\n{ex.Message}",
                    "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<Machine> GetMachinesFromDatabase()
        {
            var machines = new List<Machine>();

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();

                    var sqlBuilder = new System.Text.StringBuilder("SELECT id, machine_alias, ip_address, serial_number, location FROM machines ");
                    var cmd = new NpgsqlCommand();

                    // Check role and apply filtering
                    if (CurrentUser.Role == "deptadmin")
                    {
                        // Only filter for deptadmin users
                        if (CurrentUser.PermittedDeviceIds != null && CurrentUser.PermittedDeviceIds.Count > 0)
                        {
                            sqlBuilder.Append("WHERE id = ANY(@permittedIds) ");
                            cmd.Parameters.AddWithValue("permittedIds", CurrentUser.PermittedDeviceIds.ToArray());
                        }
                        else
                        {
                            // Deptadmin with no permitted devices - return empty list
                            return machines;
                        }
                    }
                    // For superadmin, show all machines (no WHERE clause)

                    sqlBuilder.Append("ORDER BY machine_alias");
                    cmd.CommandText = sqlBuilder.ToString();
                    cmd.Connection = connection;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            machines.Add(new Machine
                            {
                                Id = reader.GetInt32(0),
                                MachineAlias = reader.GetString(1),
                                IpAddress = reader.GetString(2),
                                SerialNumber = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                                Location = reader.IsDBNull(4) ? "" : reader.GetString(4)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في قاعدة البيانات في GetMachinesFromDatabase:\n\n{ex.Message}\n\nتتبع المكدس:\n{ex.StackTrace}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return machines;
        }

        private void SaveAttendanceLog(string badgeNumber, DateTime logTime, int machineId)
        {
            try
            {
                // Normalize badge number - remove leading zeros to match user table format
                badgeNumber = badgeNumber.TrimStart('0');
                if (string.IsNullOrEmpty(badgeNumber)) badgeNumber = "0";

                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();

                    var sql = @"INSERT INTO attendance_logs (user_badge_number, log_time, machine_id)
           VALUES (@badge, @time, @machineId)
           ON CONFLICT (user_badge_number, log_time, machine_id) DO NOTHING";


                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("badge", badgeNumber);
                        cmd.Parameters.AddWithValue("time", logTime);
                        cmd.Parameters.AddWithValue("machineId", machineId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving log: {ex.Message}");
            }
        }



        private List<AttendanceLog> GetAttendanceLogsFromDatabase()
        {
            var logs = new List<AttendanceLog>();
            using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                var sql = "SELECT log_id, user_badge_number, log_time, machine_id FROM attendance_logs ORDER BY log_time DESC LIMIT 1000";
                using (var cmd = new NpgsqlCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(new AttendanceLog
                        {
                            LogId = reader.GetInt32(0),
                            UserBadgeNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            LogTime = reader.GetDateTime(2),
                            MachineId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                        });
                    }
                }
            }
            return logs;
        }
        #endregion
    }
}
