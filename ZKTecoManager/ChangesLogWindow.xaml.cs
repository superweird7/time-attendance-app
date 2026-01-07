using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ZKTecoManager
{
    public partial class ChangesLogWindow : Window
    {
        private List<AuditLogEntry> _allChanges = new List<AuditLogEntry>();

        public ChangesLogWindow()
        {
            InitializeComponent();
            Loaded += ChangesLogWindow_Loaded;
        }

        private void ChangesLogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadChanges();
        }

        private void LoadChanges()
        {
            try
            {
                _allChanges = AuditLogger.GetRecentChanges(500, false) ?? new List<AuditLogEntry>();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل في تحميل السجل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                _allChanges = new List<AuditLogEntry>();
            }
        }

        private void ApplyFilter()
        {
            if (_allChanges == null)
            {
                _allChanges = new List<AuditLogEntry>();
            }

            var filtered = _allChanges.AsEnumerable();

            // Filter by action type (with null checks)
            if (FilterComboBox != null && FilterComboBox.SelectedItem != null)
            {
                var selectedFilter = (FilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                switch (selectedFilter)
                {
                    case "التغييرات الغير مزامنة":
                        filtered = filtered.Where(c => !c.IsSynced);
                        break;
                    case "اضافات فقط":
                        filtered = filtered.Where(c => c.ActionType?.ToUpper() == "INSERT");
                        break;
                    case "تعديلات فقط":
                        filtered = filtered.Where(c => c.ActionType?.ToUpper() == "UPDATE");
                        break;
                    case "حذف فقط":
                        filtered = filtered.Where(c => c.ActionType?.ToUpper() == "DELETE");
                        break;
                }
            }

            // Filter by table (with null checks)
            if (TableFilterComboBox != null && TableFilterComboBox.SelectedItem != null)
            {
                var selectedTable = (TableFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(selectedTable))
                {
                    filtered = filtered.Where(c => c.TableName?.ToLower() == selectedTable.ToLower());
                }
            }

            if (ChangesDataGrid != null)
            {
                ChangesDataGrid.ItemsSource = filtered.ToList();
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allChanges != null)
            {
                ApplyFilter();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadChanges();
        }

        private void ExportToTextFile_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt",
                FileName = $"changes_export_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var lines = new List<string>();
                    lines.Add($"سجل التغييرات - تم التصدير في: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    lines.Add(new string('=', 80));
                    lines.Add("");

                    var items = ChangesDataGrid.ItemsSource as List<AuditLogEntry>;
                    if (items != null)
                    {
                        foreach (var entry in items)
                        {
                            lines.Add(entry.DisplayText);
                            if (!string.IsNullOrEmpty(entry.OldValue))
                                lines.Add($"    القيمة القديمة: {entry.OldValue}");
                            if (!string.IsNullOrEmpty(entry.NewValue))
                                lines.Add($"    القيمة الجديدة: {entry.NewValue}");
                            lines.Add("");
                        }
                    }

                    File.WriteAllLines(saveDialog.FileName, lines);
                    MessageBox.Show("تم التصدير بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);

                    Process.Start(new ProcessStartInfo { FileName = saveDialog.FileName, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل في التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenLogFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = AuditLogger.ChangesFilePath;
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("ملف السجل غير موجود بعد. سيتم انشاؤه عند اول تغيير.", "معلومات", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل في فتح الملف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogFile_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("هل انت متأكد من مسح ملف السجل؟", "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    AuditLogger.ClearChangesFile();
                    MessageBox.Show("تم مسح ملف السجل بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل في المسح: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MarkAsSynced_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("هل انت متأكد من تحديد كل التغييرات كمزامنة؟", "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var unsynced = _allChanges.Where(c => !c.IsSynced).Select(c => c.LogId).ToList();
                    if (unsynced.Any())
                    {
                        AuditLogger.MarkAsSynced(unsynced);
                        LoadChanges();
                        MessageBox.Show($"تم تحديد {unsynced.Count} سجل كمزامن!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("لا توجد تغييرات غير مزامنة.", "معلومات", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل في التحديث: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
