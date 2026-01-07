using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class ManualTimeEditWindow : Window
    {
        private List<DailyReportEntry> _entries;

        public ManualTimeEditWindow(List<DailyReportEntry> entries)
        {
            InitializeComponent();
            _entries = entries;
            EntriesItemsControl.ItemsSource = _entries;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    foreach (var entry in _entries)
                    {
                        TimeSpan? clockInOverride = null;
                        if (!string.IsNullOrWhiteSpace(entry.ActualClockInString) &&
                            TimeSpan.TryParse(entry.ActualClockInString, out var parsedIn))
                        {
                            clockInOverride = parsedIn;
                        }

                        TimeSpan? clockOutOverride = null;
                        if (!string.IsNullOrWhiteSpace(entry.ActualClockOutString) &&
                            TimeSpan.TryParse(entry.ActualClockOutString, out var parsedOut))
                        {
                            clockOutOverride = parsedOut;
                        }

                        // Delete existing override
                        var deleteSql = "DELETE FROM employee_exceptions WHERE user_id_fk = @userId AND exception_date = @date";
                        using (var delCmd = new NpgsqlCommand(deleteSql, conn))
                        {
                            delCmd.Parameters.AddWithValue("userId", entry.UserId);
                            delCmd.Parameters.AddWithValue("date", entry.Date);
                            delCmd.ExecuteNonQuery();
                        }

                        // Insert new override if there are changes
                        if (clockInOverride.HasValue || clockOutOverride.HasValue)
                        {
                            var insertSql = @"INSERT INTO employee_exceptions 
                                (user_id_fk, exception_type_id_fk, exception_date, clock_in_override, clock_out_override, notes) 
                                VALUES (@userId, NULL, @date, @inOverride, @outOverride, @notes)";

                            using (var insCmd = new NpgsqlCommand(insertSql, conn))
                            {
                                insCmd.Parameters.AddWithValue("userId", entry.UserId);
                                insCmd.Parameters.AddWithValue("date", entry.Date);
                                insCmd.Parameters.AddWithValue("inOverride", (object)clockInOverride ?? DBNull.Value);
                                insCmd.Parameters.AddWithValue("outOverride", (object)clockOutOverride ?? DBNull.Value);
                                insCmd.Parameters.AddWithValue("notes", "تعديل يدوي - Manual Override");
                                insCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                MessageBox.Show("تم حفظ التعديلات بنجاح!", "نجح", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الحفظ:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
