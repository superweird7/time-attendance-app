using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager
{
    /// <summary>
    /// Backup preview window - shows backup contents before restore
    /// </summary>
    public partial class BackupPreviewWindow : Window
    {
        private readonly string _backupFilePath;
        private BackupInfo _backupInfo;

        public bool RestoreConfirmed { get; private set; } = false;

        public BackupPreviewWindow(string backupFilePath)
        {
            InitializeComponent();
            _backupFilePath = backupFilePath;
            Loaded += BackupPreviewWindow_Loaded;
        }

        private async void BackupPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await AnalyzeBackupAsync();
        }

        private async Task AnalyzeBackupAsync()
        {
            try
            {
                ShowLoading("جاري تحليل النسخة الاحتياطية...");

                _backupInfo = await Task.Run(() => ParseBackupFile(_backupFilePath));

                // Update UI
                FileNameText.Text = Path.GetFileName(_backupFilePath);
                BackupDateText.Text = _backupInfo.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "غير معروف";
                FileSizeText.Text = FormatFileSize(_backupInfo.FileSize);
                MachineNameText.Text = _backupInfo.MachineName ?? "غير معروف";
                SchemaVersionText.Text = _backupInfo.SchemaVersion ?? "غير معروف";
                TotalRecordsText.Text = _backupInfo.TotalRecords.ToString("N0");

                TablesGrid.ItemsSource = _backupInfo.Tables;

                HideLoading();
            }
            catch (Exception ex)
            {
                HideLoading();
                MessageBox.Show($"خطأ في تحليل النسخة الاحتياطية:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private BackupInfo ParseBackupFile(string filePath)
        {
            var info = new BackupInfo();
            info.FileSize = new FileInfo(filePath).Length;

            var tableRecords = new Dictionary<string, int>();
            var tableDescriptions = new Dictionary<string, string>
            {
                { "users", "الموظفين" },
                { "departments", "الأقسام" },
                { "shifts", "الورديات" },
                { "machines", "الأجهزة" },
                { "attendance_logs", "سجلات الحضور" },
                { "employee_exceptions", "استثناءات الموظفين" },
                { "exception_types", "أنواع الاستثناءات" },
                { "audit_logs", "سجلات التدقيق" },
                { "biometric_data", "البيانات البيومترية" },
                { "backup_settings", "إعدادات النسخ الاحتياطي" },
                { "sync_settings", "إعدادات المزامنة" },
                { "remote_locations", "المواقع البعيدة" },
                { "sync_history", "سجل المزامنة" },
                { "shift_rules", "قواعد الورديات" },
                { "admin_department_mappings", "صلاحيات الأقسام" },
                { "admin_device_mappings", "صلاحيات الأجهزة" },
                { "user_department_permissions", "صلاحيات المستخدمين" }
            };

            string currentTable = null;

            using (var reader = new StreamReader(filePath, System.Text.Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Parse header info
                    if (line.StartsWith("-- SCHEMA_VERSION:"))
                    {
                        info.SchemaVersion = line.Substring("-- SCHEMA_VERSION:".Length).Trim();
                    }
                    else if (line.StartsWith("-- CREATED_AT:"))
                    {
                        var dateStr = line.Substring("-- CREATED_AT:".Length).Trim();
                        if (DateTime.TryParse(dateStr, out DateTime createdAt))
                        {
                            info.CreatedAt = createdAt;
                        }
                    }
                    else if (line.StartsWith("-- MACHINE_NAME:"))
                    {
                        info.MachineName = line.Substring("-- MACHINE_NAME:".Length).Trim();
                    }
                    // Track current table
                    else if (line.StartsWith("-- TABLE:"))
                    {
                        currentTable = line.Substring("-- TABLE:".Length).Trim();
                        if (!tableRecords.ContainsKey(currentTable))
                        {
                            tableRecords[currentTable] = 0;
                        }
                    }
                    // Count INSERT statements
                    else if (line.StartsWith("INSERT INTO") && currentTable != null)
                    {
                        tableRecords[currentTable]++;
                    }
                }
            }

            // Build tables list
            info.Tables = tableRecords
                .OrderByDescending(t => t.Value)
                .Select(t => new TableInfo
                {
                    TableName = t.Key,
                    RecordCount = t.Value,
                    Description = tableDescriptions.ContainsKey(t.Key) ? tableDescriptions[t.Key] : ""
                })
                .ToList();

            info.TotalRecords = tableRecords.Values.Sum();

            return info;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
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
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"هل أنت متأكد من استعادة هذه النسخة الاحتياطية؟\n\n" +
                $"سيتم استبدال جميع البيانات الحالية بـ {_backupInfo.TotalRecords:N0} سجل.",
                "تأكيد الاستعادة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                RestoreConfirmed = true;
                DialogResult = true;
                Close();
            }
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

    /// <summary>
    /// Backup file information
    /// </summary>
    public class BackupInfo
    {
        public string SchemaVersion { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string MachineName { get; set; }
        public long FileSize { get; set; }
        public int TotalRecords { get; set; }
        public List<TableInfo> Tables { get; set; } = new List<TableInfo>();
    }

    /// <summary>
    /// Table information in backup
    /// </summary>
    public class TableInfo
    {
        public string TableName { get; set; }
        public int RecordCount { get; set; }
        public string Description { get; set; }
    }
}
