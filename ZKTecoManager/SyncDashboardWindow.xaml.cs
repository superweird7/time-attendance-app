using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZKTecoManager.Data.Repositories;
using ZKTecoManager.Models.Sync;
using ZKTecoManager.Services;

namespace ZKTecoManager
{
    /// <summary>
    /// Interaction logic for SyncDashboardWindow.xaml
    /// </summary>
    public partial class SyncDashboardWindow : Window
    {
        private readonly RemoteLocationRepository _locationRepository;
        private readonly RemoteSyncService _syncService;
        private List<RemoteLocation> _locations;

        public SyncDashboardWindow()
        {
            InitializeComponent();
            _locationRepository = new RemoteLocationRepository();
            _syncService = new RemoteSyncService();
            Loaded += SyncDashboardWindow_Loaded;
        }

        private async void SyncDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                ShowLoading("جاري تحميل البيانات...");

                // Ensure tables exist
                await _locationRepository.EnsureTableExistsAsync();

                // Load sync settings
                var settings = await _locationRepository.GetSyncSettingsAsync();
                AutoSyncCheckBox.IsChecked = settings.autoSyncEnabled;

                // Set interval combo box
                foreach (ComboBoxItem item in IntervalComboBox.Items)
                {
                    if (item.Tag?.ToString() == settings.intervalMinutes.ToString())
                    {
                        IntervalComboBox.SelectedItem = item;
                        break;
                    }
                }

                // Load locations
                await LoadLocationsAsync();

                HideLoading();
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadLocationsAsync()
        {
            _locations = await _locationRepository.GetAllAsync();
            LocationsGrid.ItemsSource = _locations;
            StatusText.Text = $"عدد المواقع: {_locations.Count}";
        }

        #region Window Controls

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Auto-Sync Settings

        private async void AutoSyncCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                var enabled = AutoSyncCheckBox.IsChecked == true;
                var interval = GetSelectedInterval();

                await _locationRepository.UpdateSyncSettingsAsync(enabled, interval);
                await AutoSyncService.Instance.UpdateSettingsAsync(enabled, interval);

                StatusText.Text = enabled ? "تم تفعيل المزامنة التلقائية" : "تم إيقاف المزامنة التلقائية";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void IntervalComboBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            try
            {
                var enabled = AutoSyncCheckBox.IsChecked == true;
                var interval = GetSelectedInterval();

                await _locationRepository.UpdateSyncSettingsAsync(enabled, interval);
                await AutoSyncService.Instance.UpdateSettingsAsync(enabled, interval);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetSelectedInterval()
        {
            var selectedItem = IntervalComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag != null && int.TryParse(selectedItem.Tag.ToString(), out int interval))
            {
                return interval;
            }
            return 15;
        }

        #endregion

        #region Location Management

        private void AddLocation_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddEditLocationWindow();
            addWindow.Owner = this;
            if (addWindow.ShowDialog() == true)
            {
                _ = LoadLocationsAsync();
            }
        }

        private void EditLocation_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null && int.TryParse(button.Tag.ToString(), out int locationId))
            {
                var location = _locations.FirstOrDefault(l => l.LocationId == locationId);
                if (location != null)
                {
                    var editWindow = new AddEditLocationWindow(location);
                    editWindow.Owner = this;
                    if (editWindow.ShowDialog() == true)
                    {
                        _ = LoadLocationsAsync();
                    }
                }
            }
        }

        private async void DeleteLocation_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null && int.TryParse(button.Tag.ToString(), out int locationId))
            {
                var location = _locations.FirstOrDefault(l => l.LocationId == locationId);
                if (location != null)
                {
                    var result = MessageBox.Show(
                        $"هل أنت متأكد من حذف الموقع '{location.LocationName}'؟",
                        "تأكيد الحذف",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            await _locationRepository.DeleteAsync(locationId);
                            await LoadLocationsAsync();
                            StatusText.Text = "تم حذف الموقع بنجاح";
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"خطأ في حذف الموقع: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        #endregion

        #region Sync Operations

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null && int.TryParse(button.Tag.ToString(), out int locationId))
            {
                var location = _locations.FirstOrDefault(l => l.LocationId == locationId);
                if (location != null)
                {
                    try
                    {
                        StatusText.Text = $"جاري اختبار الاتصال بـ {location.LocationName}...";
                        var canConnect = await _syncService.TestConnectionAsync(location);

                        if (canConnect)
                        {
                            MessageBox.Show($"تم الاتصال بنجاح بـ {location.LocationName}", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                            StatusText.Text = "الاتصال ناجح";
                        }
                        else
                        {
                            MessageBox.Show($"فشل الاتصال بـ {location.LocationName}", "فشل", MessageBoxButton.OK, MessageBoxImage.Warning);
                            StatusText.Text = "فشل الاتصال";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"خطأ في اختبار الاتصال: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        StatusText.Text = "خطأ في الاتصال";
                    }
                }
            }
        }

        private async void SyncLocation_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null && int.TryParse(button.Tag.ToString(), out int locationId))
            {
                var location = _locations.FirstOrDefault(l => l.LocationId == locationId);
                if (location != null)
                {
                    await SyncLocationAsync(location);
                }
            }
        }

        private async void SyncAll_Click(object sender, RoutedEventArgs e)
        {
            var activeLocations = _locations.Where(l => l.IsActive).ToList();
            if (!activeLocations.Any())
            {
                MessageBox.Show("لا توجد مواقع نشطة للمزامنة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowLoading("جاري مزامنة جميع المواقع...");

            foreach (var location in activeLocations)
            {
                LoadingText.Text = $"جاري مزامنة {location.LocationName}...";
                await SyncLocationAsync(location, showReviewWindow: false);
            }

            HideLoading();
            await LoadLocationsAsync();
            MessageBox.Show("تمت مزامنة جميع المواقع", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task SyncLocationAsync(RemoteLocation location, bool showReviewWindow = true)
        {
            try
            {
                ShowLoading($"جاري مزامنة {location.LocationName}...");

                // Test connection first
                var canConnect = await _syncService.TestConnectionAsync(location);
                if (!canConnect)
                {
                    HideLoading();
                    await _locationRepository.UpdateSyncStatusAsync(location.LocationId, "فشل الاتصال");
                    MessageBox.Show($"فشل الاتصال بـ {location.LocationName}", "فشل", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Fetch pending changes
                LoadingText.Text = "جاري جلب التغييرات...";
                var changes = await _syncService.FetchPendingChangesAsync(location, location.LastSyncTime);

                HideLoading();

                if (!changes.Any())
                {
                    await _locationRepository.UpdateSyncStatusAsync(location.LocationId, "لا توجد تغييرات");
                    StatusText.Text = $"لا توجد تغييرات جديدة في {location.LocationName}";
                    SyncLogger.LogNoChanges(location.LocationName);
                    await LoadLocationsAsync();
                    return;
                }

                // Log sync start
                SyncLogger.LogSyncStart(location.LocationName, changes.Count);

                // Show review window for user approval
                if (showReviewWindow)
                {
                    var reviewWindow = new SyncReviewWindow(changes, location);
                    reviewWindow.Owner = this;
                    if (reviewWindow.ShowDialog() == true)
                    {
                        // Get approved changes
                        var approvedChanges = reviewWindow.ApprovedChanges;
                        if (approvedChanges.Any())
                        {
                            ShowLoading("جاري تطبيق التغييرات...");
                            var result = await _syncService.ApplyChangesAsync(approvedChanges, location.LocationId);
                            HideLoading();

                            await _locationRepository.UpdateSyncStatusAsync(location.LocationId,
                                result.Success ? "نجاح" : "فشل جزئي");

                            StatusText.Text = $"تمت المزامنة: {result.RecordsAdded} إضافة، {result.RecordsUpdated} تحديث";

                            // Log sync result
                            SyncLogger.LogSyncResult(location.LocationName, result, approvedChanges);

                            if (result.Errors.Any())
                            {
                                MessageBox.Show($"حدثت بعض الأخطاء:\n{string.Join("\n", result.Errors.Take(5))}",
                                    "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            StatusText.Text = "تم إلغاء المزامنة";
                        }
                    }
                }
                else
                {
                    // Auto-apply non-conflict changes
                    var nonConflictChanges = changes.Where(c => c.ChangeType != ChangeType.Conflict).ToList();
                    if (nonConflictChanges.Any())
                    {
                        var result = await _syncService.ApplyChangesAsync(nonConflictChanges, location.LocationId);
                        await _locationRepository.UpdateSyncStatusAsync(location.LocationId,
                            result.Success ? "نجاح" : "فشل جزئي");

                        // Log sync result
                        SyncLogger.LogSyncResult(location.LocationName, result, nonConflictChanges);
                    }
                }

                await LoadLocationsAsync();
            }
            catch (Exception ex)
            {
                HideLoading();
                await _locationRepository.UpdateSyncStatusAsync(location.LocationId, $"خطأ: {ex.Message}");
                MessageBox.Show($"خطأ في المزامنة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                await LoadLocationsAsync();
            }
        }

        private void OpenSyncLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SyncLogger.OpenLogsFolder();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل فتح مجلد السجلات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ViewSyncHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoading("جاري تحميل سجل المزامنة...");

                var history = await LoadSyncHistoryAsync();

                HideLoading();

                if (!history.Any())
                {
                    MessageBox.Show("لا توجد سجلات مزامنة سابقة", "معلومات", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create and show history window
                var historyWindow = new SyncHistoryWindow(history);
                historyWindow.Owner = this;
                historyWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show($"خطأ في تحميل سجل المزامنة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<List<SyncHistoryEntry>> LoadSyncHistoryAsync()
        {
            return await Task.Run(() =>
            {
                var history = new List<SyncHistoryEntry>();
                using (var conn = new Npgsql.NpgsqlConnection(Infrastructure.DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"SELECT sh.sync_id, rl.location_name, sh.sync_type, sh.records_added, sh.records_updated,
                                       sh.records_skipped, sh.status, sh.error_message, sh.started_at, sh.completed_at,
                                       COALESCE(sh.duration_seconds, 0) as duration_seconds,
                                       COALESCE(sh.is_incremental, true) as is_incremental
                                FROM sync_history sh
                                LEFT JOIN remote_locations rl ON sh.location_id = rl.location_id
                                ORDER BY sh.completed_at DESC
                                LIMIT 100";

                    using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            history.Add(new SyncHistoryEntry
                            {
                                SyncId = reader.GetInt32(0),
                                LocationName = reader.IsDBNull(1) ? "غير معروف" : reader.GetString(1),
                                SyncType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                RecordsAdded = reader.GetInt32(3),
                                RecordsUpdated = reader.GetInt32(4),
                                RecordsSkipped = reader.GetInt32(5),
                                Status = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                ErrorMessage = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                StartedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                CompletedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                                DurationSeconds = reader.GetInt32(10),
                                IsIncremental = reader.GetBoolean(11)
                            });
                        }
                    }
                }
                return history;
            });
        }

        #endregion

        #region Loading Overlay

        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}
