using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class NewUsersWindow : Window
    {
        private List<string> _newUserBadges;
        private List<ExistingUserInfo> _existingUsers;

        public NewUsersWindow(int totalLogs, List<string> allBadgeNumbers)
        {
            InitializeComponent();

            // Analyze users
            AnalyzeUsers(allBadgeNumbers);

            // Update statistics
            TotalLogsText.Text = totalLogs.ToString();
            ExistingUsersText.Text = _existingUsers.Count.ToString();
            NewUsersText.Text = _newUserBadges.Count.ToString();

            // Populate grids
            ExistingUsersDataGrid.ItemsSource = _existingUsers;

            var newUsersList = _newUserBadges.GroupBy(b => b)
                .Select(g => new NewUserInfo
                {
                    BadgeNumber = g.Key,
                    LogCount = g.Count()
                })
                .OrderBy(u => u.BadgeNumber)
                .ToList();

            NewUsersDataGrid.ItemsSource = newUsersList;
        }

        private void AnalyzeUsers(List<string> allBadgeNumbers)
        {
            _existingUsers = new List<ExistingUserInfo>();
            _newUserBadges = new List<string>();

            var distinctBadges = allBadgeNumbers.Distinct().ToList();

            if (distinctBadges.Count == 0) return;

            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                // Fetch all existing users in a single query (fixes N+1 problem)
                var sql = @"SELECT u.badge_number, u.name, d.dept_name
                           FROM users u
                           LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                           WHERE u.badge_number = ANY(@badges)";

                var existingBadges = new HashSet<string>();

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("badges", distinctBadges.ToArray());
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var badge = reader.GetString(0);
                            existingBadges.Add(badge);
                            _existingUsers.Add(new ExistingUserInfo
                            {
                                BadgeNumber = badge,
                                Name = reader.GetString(1),
                                Department = reader.IsDBNull(2) ? "غير محدد" : reader.GetString(2)
                            });
                        }
                    }
                }

                // Any badge not found in the database is a new user
                foreach (var badge in distinctBadges)
                {
                    if (!existingBadges.Contains(badge))
                    {
                        _newUserBadges.Add(badge);
                    }
                }
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AddSelectedUsers_Click(object sender, RoutedEventArgs e)
        {
            var selectedUsers = NewUsersDataGrid.SelectedItems.Cast<NewUserInfo>().ToList();

            if (selectedUsers.Count == 0)
            {
                MessageBox.Show("الرجاء تحديد مستخدم واحد على الأقل", "لا يوجد تحديد",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var bulkAddWindow = new BulkAddUsersWindow(selectedUsers.Select(u => u.BadgeNumber).ToList())
            {
                Owner = this
            };

            if (bulkAddWindow.ShowDialog() == true)
            {
                // Refresh the analysis
                var allBadges = _existingUsers.Select(u => u.BadgeNumber)
                    .Concat(_newUserBadges)
                    .ToList();

                AnalyzeUsers(allBadges);

                // Update UI
                ExistingUsersText.Text = _existingUsers.Count.ToString();
                NewUsersText.Text = _newUserBadges.Count.ToString();
                ExistingUsersDataGrid.ItemsSource = null;
                ExistingUsersDataGrid.ItemsSource = _existingUsers;

                var newUsersList = _newUserBadges.GroupBy(b => b)
                    .Select(g => new NewUserInfo
                    {
                        BadgeNumber = g.Key,
                        LogCount = g.Count()
                    })
                    .OrderBy(u => u.BadgeNumber)
                    .ToList();

                NewUsersDataGrid.ItemsSource = null;
                NewUsersDataGrid.ItemsSource = newUsersList;

                MessageBox.Show($"تمت إضافة {selectedUsers.Count} مستخدم بنجاح!", "نجح",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    public class ExistingUserInfo
    {
        public string BadgeNumber { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
    }

    public class NewUserInfo
    {
        public string BadgeNumber { get; set; }
        public int LogCount { get; set; }
    }
}
