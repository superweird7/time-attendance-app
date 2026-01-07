using Microsoft.Win32;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class UserActivityLogWindow : Window
    {
        private List<ActivityLogEntry> _allLogs;

        public UserActivityLogWindow()
        {
            InitializeComponent();
            LoadUsers();
            SetDefaultDates();
            LoadActivityLogs();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadUsers()
        {
            try
            {
                var users = new List<UserItem>();
                users.Add(new UserItem { UserId = 0, Name = "الكل" });

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT user_id, name FROM users WHERE role IN ('superadmin', 'deptadmin') ORDER BY name";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new UserItem
                            {
                                UserId = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                UserFilterComboBox.ItemsSource = users;
                UserFilterComboBox.DisplayMemberPath = "Name";
                UserFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading users: {ex.Message}");
            }
        }

        private void SetDefaultDates()
        {
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
        }

        private void LoadActivityLogs()
        {
            try
            {
                _allLogs = new List<ActivityLogEntry>();

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"
                        SELECT al.log_id, al.user_id, COALESCE(u.name, 'غير معروف') as user_name,
                               al.action_type, al.table_name, al.description, al.ip_address, al.created_at,
                               al.old_value, al.new_value
                        FROM audit_logs al
                        LEFT JOIN users u ON al.user_id = u.user_id
                        WHERE al.created_at >= @fromDate AND al.created_at < @toDate
                        ORDER BY al.created_at DESC
                        LIMIT 1000";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("fromDate", FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30));
                        cmd.Parameters.AddWithValue("toDate", (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _allLogs.Add(new ActivityLogEntry
                                {
                                    LogId = reader.GetInt32(0),
                                    UserId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                                    UserName = reader.GetString(2),
                                    ActionType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    TableName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Description = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    IpAddress = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    CreatedAt = reader.GetDateTime(7),
                                    OldValue = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    NewValue = reader.IsDBNull(9) ? "" : reader.GetString(9)
                                });
                            }
                        }
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading activity logs: {ex.Message}");
                MessageBox.Show($"حدث خطأ أثناء تحميل السجلات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_allLogs == null) return;

            var filtered = new List<ActivityLogEntry>();

            // Get selected user
            var selectedUser = UserFilterComboBox.SelectedItem as UserItem;
            int? filterUserId = selectedUser?.UserId == 0 ? null : selectedUser?.UserId;

            // Get selected action type
            string filterActionType = null;
            if (ActionTypeComboBox.SelectedIndex > 0)
            {
                switch (ActionTypeComboBox.SelectedIndex)
                {
                    case 1: filterActionType = "LOGIN"; break;
                    case 2: filterActionType = "LOGOUT"; break;
                    case 3: filterActionType = "PASSWORD_CHANGE"; break;
                    case 4: filterActionType = "CREATE"; break;
                    case 5: filterActionType = "UPDATE"; break;
                    case 6: filterActionType = "DELETE"; break;
                }
            }

            // Get employee search text
            string employeeSearch = EmployeeSearchTextBox?.Text?.Trim().ToLower();

            // Get table filter
            string filterTable = null;
            if (TableFilterComboBox != null && TableFilterComboBox.SelectedIndex > 0)
            {
                filterTable = (TableFilterComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            }

            foreach (var log in _allLogs)
            {
                // Filter by user
                if (filterUserId.HasValue && log.UserId != filterUserId.Value)
                    continue;

                // Filter by action type
                if (!string.IsNullOrEmpty(filterActionType) && log.ActionType != filterActionType)
                    continue;

                // Filter by employee search (search in description, old value, and new value)
                if (!string.IsNullOrEmpty(employeeSearch))
                {
                    bool matchesSearch =
                        (log.Description?.ToLower().Contains(employeeSearch) ?? false) ||
                        (log.OldValue?.ToLower().Contains(employeeSearch) ?? false) ||
                        (log.NewValue?.ToLower().Contains(employeeSearch) ?? false) ||
                        (log.UserName?.ToLower().Contains(employeeSearch) ?? false);

                    if (!matchesSearch)
                        continue;
                }

                // Filter by table
                if (!string.IsNullOrEmpty(filterTable) && log.TableName != filterTable)
                    continue;

                filtered.Add(log);
            }

            ActivityLogDataGrid.ItemsSource = filtered;
            RecordCountText.Text = $"إجمالي السجلات: {filtered.Count}";
        }

        private void EmployeeSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TableFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ActivityLogDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActivityLogDataGrid.SelectedItem is ActivityLogEntry log)
            {
                DetailExpander.IsExpanded = true;
                OldValueTextBox.Text = string.IsNullOrEmpty(log.OldValue) ? "(لا توجد قيمة سابقة / No previous value)" : log.OldValue;
                NewValueTextBox.Text = string.IsNullOrEmpty(log.NewValue) ? "(لا توجد قيمة جديدة / No new value)" : log.NewValue;
            }
            else
            {
                DetailExpander.IsExpanded = false;
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (_allLogs != null)
            {
                LoadActivityLogs();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadActivityLogs();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logs = ActivityLogDataGrid.ItemsSource as List<ActivityLogEntry>;
                if (logs == null || logs.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
                    DefaultExt = "csv",
                    FileName = $"ActivityLog_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    // Enhanced header with before/after columns
                    sb.AppendLine("التاريخ والوقت,المستخدم,نوع النشاط,الجدول,الوصف,IP,القيمة السابقة,القيمة الجديدة");

                    foreach (var log in logs)
                    {
                        sb.AppendLine($"\"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}\",\"{log.UserName}\",\"{log.ActionTypeDisplay}\",\"{log.TableName}\",\"{log.Description?.Replace("\"", "\"\"")}\",\"{log.IpAddress}\",\"{log.OldValue?.Replace("\"", "\"\"")}\",\"{log.NewValue?.Replace("\"", "\"\"")}\")");
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"تم تصدير {logs.Count} سجل بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "هل أنت متأكد من حذف جميع سجلات النشاط؟\n\nهذا الإجراء لا يمكن التراجع عنه!",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("DELETE FROM audit_logs", conn))
                    {
                        int deleted = cmd.ExecuteNonQuery();
                        MessageBox.Show($"تم حذف {deleted} سجل بنجاح", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                // Refresh the list
                LoadActivityLogs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء الحذف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class UserItem
        {
            public int UserId { get; set; }
            public string Name { get; set; }
        }
    }

    public class ActivityLogEntry
    {
        public int LogId { get; set; }
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public string ActionType { get; set; }
        public string TableName { get; set; }
        public string Description { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public string ActionTypeDisplay
        {
            get
            {
                switch (ActionType)
                {
                    case "LOGIN": return "تسجيل دخول";
                    case "LOGOUT": return "تسجيل خروج";
                    case "PASSWORD_CHANGE": return "تغيير كلمة المرور";
                    case "CREATE": return "إنشاء";
                    case "UPDATE": return "تعديل";
                    case "DELETE": return "حذف";
                    case "BACKUP": return "نسخ احتياطي";
                    case "RESTORE": return "استعادة";
                    case "SYNC": return "مزامنة";
                    case "DOWNLOAD": return "تنزيل";
                    case "UPLOAD": return "رفع";
                    case "REPORT": return "تقرير";
                    default: return ActionType;
                }
            }
        }
    }
}
