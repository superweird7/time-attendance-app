using Npgsql;
using System;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class ChangePasswordWindow : Window
    {

        public ChangePasswordWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string currentPassword = CurrentPasswordBox.Password;
            string newPassword = NewPasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("الرجاء ملء جميع الحقول", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("كلمات المرور الجديدة غير متطابقة", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword.Length < 4)
            {
                MessageBox.Show("كلمة المرور يجب أن تكون 4 أحرف على الأقل", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT password FROM users WHERE user_id = @userId";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                        cmd.Parameters.AddWithValue("userId", CurrentUser.UserId);
                        var result = cmd.ExecuteScalar();

                        if (result == null || result == DBNull.Value)
                        {
                            MessageBox.Show("لم يتم العثور على المستخدم", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        string storedPassword = result.ToString();

                        // Verify current password using secure helper
                        if (!PasswordHelper.VerifyPassword(currentPassword, storedPassword))
                        {
                            MessageBox.Show("كلمة المرور الحالية التي أدخلتها غير صحيحة", "فشل المصادقة", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Hash the new password before storing
                    string hashedNewPassword = PasswordHelper.HashPassword(newPassword);

                    var updateSql = "UPDATE users SET password = @newPassword WHERE user_id = @userId";
                    using (var cmd = new NpgsqlCommand(updateSql, conn))
                    {
                        cmd.CommandTimeout = DatabaseConfig.DefaultCommandTimeout;
                        cmd.Parameters.AddWithValue("newPassword", hashedNewPassword);
                        cmd.Parameters.AddWithValue("userId", CurrentUser.UserId);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Log the password change
                try
                {
                    AuditLogger.Log("PASSWORD_CHANGE", "users", CurrentUser.UserId, null, null, "User changed their password");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Audit log failed: {logEx.Message}");
                }

                MessageBox.Show("تم تغيير كلمة المرور بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            catch (NpgsqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database error changing password: {ex.Message}");
                MessageBox.Show($"حدث خطأ في قاعدة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error changing password: {ex.Message}");
                MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
