using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class EmployeeHistoryWindow : Window
    {
        private readonly string _badgeNumber;
        private readonly string _employeeName;

        public EmployeeHistoryWindow(string badgeNumber, string employeeName)
        {
            InitializeComponent();
            _badgeNumber = badgeNumber;
            _employeeName = employeeName;

            EmployeeNameText.Text = employeeName;
            BadgeNumberText.Text = badgeNumber;

            Loaded += EmployeeHistoryWindow_Loaded;
        }

        private void EmployeeHistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                var history = GetEmployeeHistory(_badgeNumber);
                HistoryDataGrid.ItemsSource = history;
                TotalChangesText.Text = history.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load history: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<AuditLogEntry> GetEmployeeHistory(string badgeNumber)
        {
            var logs = new List<AuditLogEntry>();

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Search for audit logs related to this employee
                    // Look in description, old_value, or new_value for the badge number
                    var sql = @"
                        SELECT al.log_id, al.user_id, u.name as user_name, al.action_type, al.table_name,
                               al.record_id, al.old_value, al.new_value, al.ip_address,
                               al.description, al.created_at, COALESCE(al.is_synced, false)
                        FROM audit_logs al
                        LEFT JOIN users u ON al.user_id = u.user_id
                        WHERE al.table_name = 'users'
                          AND (
                              al.description LIKE @search1
                              OR al.old_value LIKE @search2
                              OR al.new_value LIKE @search3
                              OR al.description LIKE @search4
                          )
                        ORDER BY al.created_at DESC
                        LIMIT 500";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("search1", $"%{badgeNumber}%");
                        cmd.Parameters.AddWithValue("search2", $"%{badgeNumber}%");
                        cmd.Parameters.AddWithValue("search3", $"%{badgeNumber}%");
                        cmd.Parameters.AddWithValue("search4", $"%{_employeeName}%");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                logs.Add(new AuditLogEntry
                                {
                                    LogId = reader.GetInt32(0),
                                    UserId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                    UserName = reader.IsDBNull(2) ? "System" : reader.GetString(2),
                                    ActionType = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    TableName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    RecordId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                                    OldValue = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    NewValue = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    IpAddress = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                    CreatedAt = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                    IsSynced = reader.GetBoolean(11)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting employee history: {ex.Message}");
            }

            return logs;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
