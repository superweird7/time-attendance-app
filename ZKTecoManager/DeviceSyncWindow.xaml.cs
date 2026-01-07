using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using zkemkeeper;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class DeviceSyncWindow : Window
    {
        private Machine _selectedMachine;
        private CZKEM zk = new CZKEM();

        // Helper class to display fingerprint info in the ComboBox
        private class FingerprintRecord
        {
            public int FingerIndex { get; set; }
            public string TemplateData { get; set; }
            public string DisplayText => $"Finger Index: {FingerIndex}";
        }

        public DeviceSyncWindow(Machine selectedMachine)
        {
            InitializeComponent();
            _selectedMachine = selectedMachine;
            DeviceInfoTextBlock.Text = $"مزامنة مع الجهاز: {_selectedMachine.MachineAlias} ({_selectedMachine.IpAddress})";
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load users from our local database into the ComboBox for the upload feature
            LoadUsersIntoComboBox();
        }

        private void DownloadUsers_Click(object sender, RoutedEventArgs e)
        {
            if (!zk.Connect_Net(_selectedMachine.IpAddress, 4370))
            {
                MessageBox.Show("تعذر الاتصال بالجهاز", "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.Cursor = Cursors.Wait;
            zk.EnableDevice(1, false); // Disable device for stable data transfer

            int usersAdded = 0;
            int templatesAdded = 0;

            try
            {
                var existingBadgeNumbers = new HashSet<string>();
                if (DownloadNewUsersRadio.IsChecked == true)
                {
                    existingBadgeNumbers = GetExistingBadgeNumbersFromDb();
                }

                // Variables for the SDK method
                string badgeNumber, name, password;
                int privilege;
                bool enabled;

                zk.ReadAllUserID(1); // Read all user IDs into the device's memory

                while (zk.SSR_GetAllUserInfo(1, out badgeNumber, out name, out password, out privilege, out enabled))
                {
                    if (DownloadNewUsersRadio.IsChecked == true && existingBadgeNumbers.Contains(badgeNumber))
                    {
                        continue; // Skip user if they already exist in our DB
                    }

                    int currentUserId = SaveUserAndGetId(badgeNumber, name);
                    if (currentUserId == -1) continue;
                    usersAdded++;

                    if (IncludeFingerprintsCheckBox.IsChecked == true)
                    {
                        templatesAdded += DownloadFingerprintsForUser(currentUserId, badgeNumber);
                    }
                    if (IncludeFacesCheckBox.IsChecked == true)
                    {
                        templatesAdded += DownloadFaceTemplateForUser(currentUserId, badgeNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء التحميل:\n\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                zk.EnableDevice(1, true);
                zk.Disconnect();
                this.Cursor = Cursors.Arrow;
                MessageBox.Show($"اكتمل التحميل.\n\nالمستخدمون الجدد/المحدثون: {usersAdded}\nالقوالب البيومترية المحملة: {templatesAdded}", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UploadFingerprint_Click(object sender, RoutedEventArgs e)
        {
            if (UsersComboBox.SelectedItem is User selectedUser && FingerprintsComboBox.SelectedItem is FingerprintRecord selectedFingerprint)
            {
                if (!zk.Connect_Net(_selectedMachine.IpAddress, 4370))
                {
                    MessageBox.Show("تعذر الاتصال بالجهاز", "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                this.Cursor = Cursors.Wait;
                zk.EnableDevice(1, false);

                try
                {
                    // Upload the selected template to the device for the selected user
                    if (zk.SetUserTmpExStr(1, selectedUser.BadgeNumber, selectedFingerprint.FingerIndex, 1, selectedFingerprint.TemplateData))
                    {
                        MessageBox.Show($"تم رفع البصمة لـ {selectedUser.Name} (الفهرس: {selectedFingerprint.FingerIndex}) بنجاح", "نجح الرفع", MessageBoxButton.OK, MessageBoxImage.Information);
                        zk.RefreshData(1); // Refresh device's internal data
                    }
                    else
                    {
                        int errorCode = 0;
                        zk.GetLastError(ref errorCode);
                        MessageBox.Show($"فشل رفع البصمة. رمز الخطأ: {errorCode}", "فشل الرفع", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    zk.EnableDevice(1, true);
                    zk.Disconnect();
                    this.Cursor = Cursors.Arrow;
                }
            }
            else
            {
                MessageBox.Show("الرجاء اختيار مستخدم وبصمة محفوظة للرفع", "مطلوب تحديد", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #region Helper Methods

        private HashSet<string> GetExistingBadgeNumbersFromDb()
        {
            var badgeNumbers = new HashSet<string>();
            using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                var sql = "SELECT badge_number FROM users";
                using (var cmd = new NpgsqlCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0)) badgeNumbers.Add(reader.GetString(0));
                    }
                }
            }
            return badgeNumbers;
        }

        private int SaveUserAndGetId(string badgeNumber, string name)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                var sql = "INSERT INTO users (badge_number, name) VALUES (@badge, @name) ON CONFLICT (badge_number) DO UPDATE SET name = @name RETURNING user_id";
                using (var cmd = new NpgsqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("badge", badgeNumber);
                    cmd.Parameters.AddWithValue("name", name);
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        private int DownloadFingerprintsForUser(int userId, string badgeNumber)
        {
            int templatesFound = 0;
            for (int fingerIndex = 0; fingerIndex < 10; fingerIndex++)
            {
                string fingerTemplate;
                int templateLength;
                int flag;

                if (zk.GetUserTmpExStr(1, badgeNumber, fingerIndex, out flag, out fingerTemplate, out templateLength))
                {
                    SaveBiometricData(userId, fingerIndex, fingerTemplate, 1); // Type 1 for Fingerprint
                    templatesFound++;
                }
            }
            return templatesFound;
        }

        private int DownloadFaceTemplateForUser(int userId, string badgeNumber)
        {
            string faceTemplate = "";
            int faceLength = 0;

            if (zk.GetUserFaceStr(1, badgeNumber, 50, ref faceTemplate, ref faceLength))
            {
                SaveBiometricData(userId, 50, faceTemplate, 2); // Type 2 for Face
                return 1;
            }
            return 0;
        }

        private void SaveBiometricData(int userId, int templateIndex, string templateData, int biometricType)
        {
            using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                var sql = "INSERT INTO biometric_data (user_id_fk, finger_index, template_data, biometric_type) VALUES (@userId, @fingerIndex, @template, @type) ON CONFLICT DO NOTHING";
                using (var cmd = new NpgsqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    cmd.Parameters.AddWithValue("fingerIndex", templateIndex);
                    cmd.Parameters.AddWithValue("template", templateData);
                    cmd.Parameters.AddWithValue("type", biometricType);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LoadUsersIntoComboBox()
        {
            var users = new List<User>();
            using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                var sql = "SELECT user_id, badge_number, name FROM users WHERE name IS NOT NULL ORDER BY name";
                using (var cmd = new NpgsqlCommand(sql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new User { UserId = reader.GetInt32(0), BadgeNumber = reader.GetString(1), Name = reader.GetString(2) });
                    }
                }
            }
            UsersComboBox.ItemsSource = users;
        }

        private void UsersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FingerprintsComboBox.ItemsSource = null;
            if (UsersComboBox.SelectedItem is User selectedUser)
            {
                var fingerprints = new List<FingerprintRecord>();
                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();
                    var sql = "SELECT finger_index, template_data FROM biometric_data WHERE user_id_fk = @userId AND biometric_type = 1 ORDER BY finger_index";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                fingerprints.Add(new FingerprintRecord { FingerIndex = reader.GetInt32(0), TemplateData = reader.GetString(1) });
                            }
                        }
                    }
                }
                FingerprintsComboBox.ItemsSource = fingerprints;
            }
        }
        #endregion
    }
}
