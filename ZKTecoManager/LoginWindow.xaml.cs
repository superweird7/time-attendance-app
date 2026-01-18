using Microsoft.Win32;
using Npgsql;
using System;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class LoginWindow : Window
    {

        public LoginWindow()
        {
            InitializeComponent();

            // Focus on username textbox when window loads
            Loaded += (s, e) => UsernameTextBox.Focus();
        }

        // Allow dragging window
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("الرجاء إدخال اسم المستخدم وكلمة المرور", "خطأ في تسجيل الدخول",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                User user = GetUserByCredentials(username, password);

                if (user != null)
                {
                    // Check if user has permission to login (superadmin, deptadmin, or leaveadmin)
                    var validRoles = new[] { "superadmin", "deptadmin", "leaveadmin" };
                    var userRole = user.Role.Trim().ToLower();
                    bool isValidRole = false;
                    foreach (var role in validRoles)
                    {
                        if (userRole == role) { isValidRole = true; break; }
                    }

                    if (!isValidRole)
                    {
                        MessageBox.Show(
                            "ليس لديك صلاحية الدخول إلى النظام\nفقط المدراء يمكنهم تسجيل الدخول",
                            "رفض الوصول",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        // Clear password field
                        PasswordBox.Clear();
                        UsernameTextBox.Focus();
                        return;
                    }

                    // تسجيل الدخول ناجح - Set user with permissions
                    CurrentUser.SetUser(user, user.CanEditTimes);
                    CurrentUser.SystemAccessType = user.SystemAccessType ?? "full_access";

                    // Load permissions based on role
                    if (userRole == "deptadmin")
                    {
                        LoadDeptAdminPermissions(user.UserId);
                    }
                    else if (userRole == "superadmin")
                    {
                        // Superadmin always has edit permission
                        CurrentUser.CanEditTimes = true;
                    }
                    else if (userRole == "leaveadmin")
                    {
                        // Leaveadmin: Load department permissions for filtering
                        LoadDeptAdminPermissions(user.UserId);
                    }

                    // Open appropriate window based on role
                    if (userRole == "leaveadmin")
                    {
                        // Leaveadmin goes directly to Leave Management
                        var leaveWindow = new LeaveManagementWindow();
                        leaveWindow.Show();
                    }
                    else
                    {
                        // Superadmin and deptadmin go to MainWindow
                        MainWindow mainWindow = new MainWindow();
                        mainWindow.Show();
                    }
                    this.Close();
                }
                else
                {
                    // Debug: Show what was entered
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Login failed - Username: '{username}', Password length: {password.Length}");

                    MessageBox.Show($"اسم المستخدم أو كلمة المرور غير صحيحة\n\nDebug: Tried username '{username}'", "خطأ في تسجيل الدخول",
                        MessageBoxButton.OK, MessageBoxImage.Error);

                    // مسح الحقول
                    PasswordBox.Clear();
                    UsernameTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ في قاعدة البيانات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("هل أنت متأكد من الخروج من البرنامج؟",
                "تأكيد الخروج",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "اختر ملف النسخة الاحتياطية - Select Backup File",
                Filter = "SQL Backup Files (*.sql)|*.sql|All Files (*.*)|*.*",
                InitialDirectory = BackupManager.GetBackupPath()
            };

            if (openFileDialog.ShowDialog() == true)
            {
                BackupManager.RestoreBackup(openFileDialog.FileName);
            }
        }

        #region Database Methods
        private User GetUserByCredentials(string username, string password)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Attempting login for username: '{username}'");

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Database connection opened successfully");

                    // Use TRIM in SQL to ignore extra spaces - search by badge_number
                    var sql = "SELECT user_id, name, password, role, can_edit_times, system_access_type FROM users WHERE TRIM(badge_number) = @username";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                        cmd.Parameters.AddWithValue("username", username);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedPassword = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                                System.Diagnostics.Debug.WriteLine($"[LOGIN] User found. Stored password length: {storedPassword.Length}");

                                // Secure password verification (supports both hashed and legacy plaintext)
                                bool passwordMatch = PasswordHelper.VerifyPassword(password, storedPassword);
                                System.Diagnostics.Debug.WriteLine($"[LOGIN] Password verification result: {passwordMatch}");

                                if (passwordMatch)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Login successful for user: {reader.GetString(1)}");
                                    return new User
                                    {
                                        UserId = reader.GetInt32(0),
                                        Name = reader.GetString(1).Trim(),
                                        Role = reader.IsDBNull(3) ? "user" : reader.GetString(3).Trim(),
                                        CanEditTimes = reader.IsDBNull(4) ? false : reader.GetBoolean(4),
                                        SystemAccessType = reader.IsDBNull(5) ? "full_access" : reader.GetString(5).Trim()
                                    };
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[LOGIN] No user found with username: '{username}'");
                            }
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Database error: {ex.Message}");
                throw new Exception("حدث خطأ في الاتصال بقاعدة البيانات", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] General error: {ex.Message}");
                throw;
            }
            return null;
        }



        private void LoadDeptAdminPermissions(int userId)
        {
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                // تحميل صلاحيات الأقسام
                var deptSql = @"SELECT department_id_fk 
                               FROM admin_department_mappings 
                               WHERE user_id_fk = @userId";

                using (var cmd = new NpgsqlCommand(deptSql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CurrentUser.PermittedDepartmentIds.Add(reader.GetInt32(0));
                        }
                    }
                }

                // تحميل صلاحيات الأجهزة
                var devSql = @"SELECT device_id_fk 
                              FROM admin_device_mappings 
                              WHERE user_id_fk = @userId";

                using (var cmd = new NpgsqlCommand(devSql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CurrentUser.PermittedDeviceIds.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
        }
        #endregion
    }
}
