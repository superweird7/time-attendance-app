using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using zkemkeeper;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    // Helper class for building the department tree
    public class DepartmentNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }
        public List<DepartmentNode> Children { get; set; } = new List<DepartmentNode>();
    }

    // Helper class for retrieving fingerprint data
    public class FingerprintRecord
    {
        public int FingerIndex { get; set; }
        public string TemplateData { get; set; }
    }

    public partial class FromPCToDeviceWindow : Window
    {
        private List<User> allUsers = new List<User>(); // Stores the complete list of users for filtering

        public FromPCToDeviceWindow()
        {
            InitializeComponent();
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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Show("جاري تحميل البيانات...", "الرجاء الانتظار");
            try
            {
                // When the window loads, fill all UI elements with data from our local database
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() => LoadDepartmentsIntoTreeView());
                    Dispatcher.Invoke(() => LoadUsersFromDatabase());
                    Dispatcher.Invoke(() => LoadDevicesFromDatabase());
                });
            }
            finally
            {
                LoadingOverlay.Hide();
            }
        }

        private void UserSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = UserSearchTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // If the search box is empty, show all users
                UsersGrid.ItemsSource = allUsers;
            }
            else
            {
                // Filter the list based on name or badge number
                var filteredUsers = allUsers.Where(user =>
                    (user.Name != null && user.Name.ToLower().Contains(searchText)) ||
                    (user.BadgeNumber != null && user.BadgeNumber.Contains(searchText))
                ).ToList();
                UsersGrid.ItemsSource = filteredUsers;
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedUsers = UsersGrid.SelectedItems.Cast<User>().ToList();
            var selectedDevices = DevicesGrid.SelectedItems.Cast<Machine>().ToList();

            if (selectedUsers.Count == 0 || selectedDevices.Count == 0)
            {
                MessageBox.Show("الرجاء اختيار مستخدم واحد على الأقل وجهاز واحد للرفع إليه", "مطلوب تحديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool uploadUserInfo = UserInfoCheckBox.IsChecked == true;
            bool uploadFingerprints = FingerprintCheckBox.IsChecked == true;

            LoadingOverlay.Show("جاري رفع البيانات...", $"رفع {selectedUsers.Count} مستخدم إلى {selectedDevices.Count} جهاز");
            this.Cursor = Cursors.Wait;

            try
            {
                var log = await Task.Run(() =>
                {
                    var logBuilder = new StringBuilder();
                    var zk = new CZKEM();

                    foreach (var device in selectedDevices)
                    {
                        logBuilder.AppendLine($"--- الاتصال بالجهاز: {device.MachineAlias} ({device.IpAddress}) ---");

                        Dispatcher.Invoke(() => LoadingOverlay.UpdateStatus($"جاري الاتصال بـ {device.MachineAlias}..."));

                        if (!zk.Connect_Net(device.IpAddress, 4370))
                        {
                            logBuilder.AppendLine("    فشل الاتصال.\n");
                            continue; // Skip to the next device
                        }

                        zk.EnableDevice(1, false); // Disable device during upload

                        try
                        {
                            int userCount = 0;
                            foreach (var user in selectedUsers)
                            {
                                userCount++;
                                Dispatcher.Invoke(() => LoadingOverlay.UpdateStatus($"رفع المستخدم {userCount}/{selectedUsers.Count}: {user.Name}"));

                                logBuilder.AppendLine($"  رفع المستخدم: {user.Name} (البطاقة: {user.BadgeNumber})");

                                // 1. Upload User Info
                                if (uploadUserInfo)
                                {
                                    if (zk.SSR_SetUserInfo(1, user.BadgeNumber, user.Name, "", 0, true))
                                    {
                                        logBuilder.AppendLine("    - تم رفع معلومات المستخدم بنجاح.");
                                    }
                                    else
                                    {
                                        logBuilder.AppendLine("    - فشل رفع معلومات المستخدم.");
                                    }
                                }

                                // 2. Upload Fingerprints
                                if (uploadFingerprints)
                                {
                                    var fingerprints = GetFingerprintsForUser(user.UserId);
                                    if (fingerprints.Count == 0)
                                    {
                                        logBuilder.AppendLine("    - لم يتم العثور على بصمات محفوظة لهذا المستخدم.");
                                    }
                                    else
                                    {
                                        foreach (var fp in fingerprints)
                                        {
                                            if (zk.SetUserTmpExStr(1, user.BadgeNumber, fp.FingerIndex, 1, fp.TemplateData))
                                            {
                                                logBuilder.AppendLine($"    - تم رفع البصمة (الفهرس: {fp.FingerIndex}) بنجاح.");
                                            }
                                            else
                                            {
                                                logBuilder.AppendLine($"    - فشل رفع البصمة (الفهرس: {fp.FingerIndex}).");
                                            }
                                        }
                                    }
                                }
                            }
                            zk.RefreshData(1); // Tell the device to refresh its internal data
                        }
                        finally
                        {
                            zk.EnableDevice(1, true); // Re-enable the device
                            zk.Disconnect();
                            logBuilder.AppendLine("    تم قطع الاتصال.\n");
                        }
                    }

                    return logBuilder.ToString();
                });

                // Audit log the upload operation
                string uploadDetails = "";
                if (uploadUserInfo && uploadFingerprints)
                    uploadDetails = "Users + Fingerprints";
                else if (uploadUserInfo)
                    uploadDetails = "Users only";
                else if (uploadFingerprints)
                    uploadDetails = "Fingerprints only";

                AuditLogger.Log(
                    "UPLOAD",
                    "users",
                    null,
                    null,
                    $"Devices: {selectedDevices.Count}, Users: {selectedUsers.Count}, Type: {uploadDetails}",
                    $"Uploaded {selectedUsers.Count} users ({uploadDetails}) to {selectedDevices.Count} device(s)"
                );

                MessageBox.Show(log, "اكتملت عملية الرفع", MessageBoxButton.OK, MessageBoxImage.Information);
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

        #region Data Loading Methods
        private void LoadDepartmentsIntoTreeView()
        {
            try
            {
                var departments = new List<DepartmentNode>();
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT dept_id, dept_name, parent_dept_id FROM departments";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            departments.Add(new DepartmentNode
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                ParentId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                            });
                        }
                    }
                }

                var departmentLookup = departments.ToDictionary(d => d.Id);
                var rootDepartments = new List<DepartmentNode>();
                foreach (var dept in departments)
                {
                    if (dept.ParentId != 0 && departmentLookup.ContainsKey(dept.ParentId))
                    {
                        departmentLookup[dept.ParentId].Children.Add(dept);
                    }
                    else
                    {
                        rootDepartments.Add(dept);
                    }
                }
                DepartmentTreeView.ItemsSource = rootDepartments;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل الأقسام: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadUsersFromDatabase()
        {
            try
            {
                allUsers.Clear();
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT user_id, name, badge_number, default_dept_id FROM users ORDER BY name";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allUsers.Add(new User { UserId = reader.GetInt32(0), Name = reader.GetString(1), BadgeNumber = reader.GetString(2), DefaultDeptId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3) });
                        }
                    }
                }
                UsersGrid.ItemsSource = allUsers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل المستخدمين من قاعدة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDevicesFromDatabase()
        {
            try
            {
                var machines = new List<Machine>();
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT id, machine_alias, ip_address FROM machines ORDER BY machine_alias";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            machines.Add(new Machine { Id = reader.GetInt32(0), MachineAlias = reader.GetString(1), IpAddress = reader.GetString(2) });
                        }
                    }
                }
                DevicesGrid.ItemsSource = machines;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل الأجهزة من قاعدة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<FingerprintRecord> GetFingerprintsForUser(int userId)
        {
            var fingerprints = new List<FingerprintRecord>();
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sql = "SELECT finger_index, template_data FROM biometric_data WHERE user_id_fk = @userId AND biometric_type = 1";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fingerprints.Add(new FingerprintRecord
                            {
                                FingerIndex = reader.GetInt32(0),
                                TemplateData = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            return fingerprints;
        }
        #endregion
    }
}
