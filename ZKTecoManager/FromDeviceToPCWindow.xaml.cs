using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using zkemkeeper;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class FromDeviceToPCWindow : Window
    {
        private Machine _selectedMachine;

        public FromDeviceToPCWindow(Machine selectedMachine)
        {
            InitializeComponent();
            _selectedMachine = selectedMachine;
            DeviceListBox.ItemsSource = new List<Machine> { _selectedMachine };
            DeviceListBox.SelectedIndex = 0;
        }

        // *** ADDED: Allow dragging window ***
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // *** ADDED: Close button handler ***
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void BrowseDeviceUsers_Click(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Show("جاري الاتصال بالجهاز...", "الرجاء الانتظار");
            this.Cursor = Cursors.Wait;

            try
            {
                // --- 1. Get existing users from our local database ---
                var localUsers = await Task.Run(() => GetUsersFromDatabase());
                var localBadgeNumbers = new HashSet<string>(localUsers.Select(u => u.BadgeNumber));
                LocalUsersGrid.ItemsSource = localUsers;

                LoadingOverlay.UpdateStatus("جاري قراءة المستخدمين من الجهاز...");

                // --- 2. Connect to the device and get all users ---
                var newUsersFromDevice = await Task.Run(() =>
                {
                    var zk = new CZKEM();
                    if (!zk.Connect_Net(_selectedMachine.IpAddress, 4370))
                    {
                        return null; // Connection failed
                    }

                    // --- 3. Loop through device users and sort them ---
                    var newUsers = new List<User>();

                    string badgeNumber, name, password;
                    int privilege;
                    bool enabled;

                    zk.EnableDevice(1, false);
                    zk.ReadAllUserID(1);
                    while (zk.SSR_GetAllUserInfo(1, out badgeNumber, out name, out password, out privilege, out enabled))
                    {
                        // If user is NOT in our local DB, add them to the "New User" list
                        if (!localBadgeNumbers.Contains(badgeNumber))
                        {
                            newUsers.Add(new User { Name = name, BadgeNumber = badgeNumber });
                        }
                    }
                    zk.EnableDevice(1, true);
                    zk.Disconnect();

                    return newUsers;
                });

                if (newUsersFromDevice == null)
                {
                    MessageBox.Show("تعذر الاتصال بالجهاز", "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                NewUsersGrid.ItemsSource = newUsersFromDevice;
                MessageBox.Show($"تم العثور على {newUsersFromDevice.Count} مستخدم جديد على الجهاز", "نتائج البحث", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Hide();
                this.Cursor = Cursors.Arrow;
            }
        }

        private List<User> GetUsersFromDatabase()
        {
            var users = new List<User>();
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sql = "SELECT user_id, name, badge_number FROM users";
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new User { UserId = reader.GetInt32(0), Name = reader.GetString(1), BadgeNumber = reader.GetString(2) });
                    }
                }
            }
            return users;
        }
    }
}
