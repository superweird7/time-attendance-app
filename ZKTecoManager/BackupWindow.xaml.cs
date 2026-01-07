using Microsoft.Win32;
using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class BackupWindow : Window
    {

        public BackupWindow()
        {
            InitializeComponent();
            LoadSettings();
            LoadLastBackupInfo();
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

        #region Backup/Restore

        private void BrowseBackupPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "اختر مسار حفظ النسخ الاحتياطية";

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackupPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            string backupPath = BackupPathTextBox.Text;

            if (string.IsNullOrWhiteSpace(backupPath))
            {
                MessageBox.Show("الرجاء اختيار مسار الحفظ", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.Cursor = Cursors.Wait;

            bool success = BackupManager.CreateBackup(backupPath);

            this.Cursor = Cursors.Arrow;

            if (success)
            {
                LoadLastBackupInfo();
            }
        }

        private void BrowseRestoreFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "ZKTeco Backup Files (*.zkbak)|*.zkbak|SQL Backup Files (*.sql)|*.sql|All Files (*.*)|*.*";
            dialog.Title = "اختر ملف النسخة الاحتياطية / Select Backup File";

            if (dialog.ShowDialog() == true)
            {
                RestoreFileTextBox.Text = dialog.FileName;
            }
        }

        private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            string restoreFile = RestoreFileTextBox.Text;

            if (string.IsNullOrWhiteSpace(restoreFile) || !File.Exists(restoreFile))
            {
                MessageBox.Show("الرجاء اختيار ملف نسخة احتياطية صحيح", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show backup preview window first
            var previewWindow = new BackupPreviewWindow(restoreFile);
            previewWindow.Owner = this;

            if (previewWindow.ShowDialog() != true || !previewWindow.RestoreConfirmed)
            {
                return; // User cancelled
            }

            // Show loading overlay
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "جاري الاستعادة...";
            this.Cursor = Cursors.Wait;

            bool success = await Task.Run(() => BackupManager.RestoreBackup(restoreFile));

            // Hide loading overlay
            LoadingOverlay.Visibility = Visibility.Collapsed;
            this.Cursor = Cursors.Arrow;

            if (success)
            {
                MessageBox.Show("يجب إعادة تشغيل البرنامج لتطبيق التغييرات", "تم الاستعادة", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
            }
        }

        private void VerifyBackup_Click(object sender, RoutedEventArgs e)
        {
            string backupFile = RestoreFileTextBox.Text;

            if (string.IsNullOrWhiteSpace(backupFile))
            {
                MessageBox.Show("الرجاء اختيار ملف نسخة احتياطية أولاً", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(backupFile))
            {
                MessageBox.Show("ملف النسخة الاحتياطية غير موجود", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.Cursor = Cursors.Wait;

            var result = BackupManager.VerifyBackup(backupFile);

            this.Cursor = Cursors.Arrow;

            string summary = result.GetSummary();

            MessageBox.Show(
                summary,
                result.IsValid ? "نتيجة التحقق - صالح" : "نتيجة التحقق - غير صالح",
                MessageBoxButton.OK,
                result.IsValid ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void LoadLastBackupInfo()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT last_backup_date FROM backup_settings LIMIT 1";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            DateTime lastBackup = (DateTime)result;
                            LastBackupText.Text = $"آخر نسخة احتياطية: {lastBackup:yyyy-MM-dd HH:mm}";
                        }
                        else
                        {
                            LastBackupText.Text = "آخر نسخة احتياطية: لا توجد";
                        }
                    }
                }
            }
            catch
            {
                LastBackupText.Text = "آخر نسخة احتياطية: غير متوفر";
            }
        }

        #endregion

        #region Settings

        private void LoadSettings()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"SELECT auto_backup_enabled, backup_time, backup_retention_days, backup_path,
                                       server_backup_enabled, server_backup_path, server_backup_interval_days, last_server_backup_date
                                FROM backup_settings LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                AutoBackupCheckBox.IsChecked = reader.GetBoolean(0);
                                BackupTimeTextBox.Text = reader.GetTimeSpan(1).ToString(@"hh\:mm");
                                RetentionDaysTextBox.Text = reader.GetInt32(2).ToString();
                                BackupPathTextBox.Text = reader.GetString(3);

                                // Server backup settings
                                try
                                {
                                    ServerBackupCheckBox.IsChecked = !reader.IsDBNull(4) && reader.GetBoolean(4);
                                    ServerBackupPathTextBox.Text = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                    ServerBackupIntervalTextBox.Text = reader.IsDBNull(6) ? "10" : reader.GetInt32(6).ToString();

                                    if (!reader.IsDBNull(7))
                                    {
                                        DateTime lastServerBackup = reader.GetDateTime(7);
                                        LastServerBackupText.Text = $"آخر نسخة للسيرفر: {lastServerBackup:yyyy-MM-dd HH:mm}";
                                    }
                                    else
                                    {
                                        LastServerBackupText.Text = "آخر نسخة للسيرفر: لا توجد";
                                    }
                                }
                                catch
                                {
                                    // Columns might not exist in older database
                                    ServerBackupCheckBox.IsChecked = false;
                                    ServerBackupPathTextBox.Text = "";
                                    ServerBackupIntervalTextBox.Text = "10";
                                    LastServerBackupText.Text = "آخر نسخة للسيرفر: لا توجد";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الإعدادات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!TimeSpan.TryParse(BackupTimeTextBox.Text, out TimeSpan backupTime))
                {
                    MessageBox.Show("صيغة الوقت غير صحيحة. استخدم الصيغة HH:mm", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(RetentionDaysTextBox.Text, out int retentionDays) || retentionDays < 1)
                {
                    MessageBox.Show("عدد أيام الاحتفاظ يجب أن يكون رقم أكبر من 0", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate server backup interval
                if (!int.TryParse(ServerBackupIntervalTextBox.Text, out int serverBackupInterval) || serverBackupInterval < 1)
                {
                    serverBackupInterval = 10; // Default to 10 days
                }

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = @"UPDATE backup_settings
                               SET auto_backup_enabled = @enabled,
                                   backup_time = @time,
                                   backup_retention_days = @days,
                                   backup_path = @path,
                                   server_backup_enabled = @serverEnabled,
                                   server_backup_path = @serverPath,
                                   server_backup_interval_days = @serverInterval,
                                   last_modified = @modified";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("enabled", AutoBackupCheckBox.IsChecked ?? false);
                        cmd.Parameters.AddWithValue("time", backupTime);
                        cmd.Parameters.AddWithValue("days", retentionDays);
                        cmd.Parameters.AddWithValue("path", BackupPathTextBox.Text);
                        cmd.Parameters.AddWithValue("serverEnabled", ServerBackupCheckBox.IsChecked ?? false);
                        cmd.Parameters.AddWithValue("serverPath", (object)ServerBackupPathTextBox.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("serverInterval", serverBackupInterval);
                        cmd.Parameters.AddWithValue("modified", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("تم حفظ الإعدادات بنجاح!", "نجح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الإعدادات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseServerPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "اختر مجلد النسخ الاحتياطي على السيرفر";
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ServerBackupPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void TestServerConnection_Click(object sender, RoutedEventArgs e)
        {
            string serverPath = ServerBackupPathTextBox.Text;

            if (string.IsNullOrWhiteSpace(serverPath))
            {
                MessageBox.Show("الرجاء إدخال مسار المجلد على السيرفر", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                this.Cursor = System.Windows.Input.Cursors.Wait;

                // Check if path exists
                if (Directory.Exists(serverPath))
                {
                    // Try to create a test file
                    string testFile = Path.Combine(serverPath, $"test_connection_{Environment.MachineName}.tmp");
                    File.WriteAllText(testFile, "Test connection from ZKTeco Manager");
                    File.Delete(testFile);

                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    MessageBox.Show($"✅ تم الاتصال بالسيرفر بنجاح!\n\nالمسار: {serverPath}", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Try to create the directory
                    try
                    {
                        Directory.CreateDirectory(serverPath);

                        // Test write access
                        string testFile = Path.Combine(serverPath, $"test_connection_{Environment.MachineName}.tmp");
                        File.WriteAllText(testFile, "Test connection from ZKTeco Manager");
                        File.Delete(testFile);

                        this.Cursor = System.Windows.Input.Cursors.Arrow;
                        MessageBox.Show($"✅ تم إنشاء المجلد والاتصال بنجاح!\n\nالمسار: {serverPath}", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception createEx)
                    {
                        this.Cursor = System.Windows.Input.Cursors.Arrow;
                        MessageBox.Show($"❌ فشل الاتصال بالسيرفر!\n\nالسبب: {createEx.Message}\n\nتأكد من:\n• أن المسار صحيح\n• أن المجلد المشترك موجود\n• أن لديك صلاحيات الكتابة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                MessageBox.Show($"❌ فشل الاتصال بالسيرفر!\n\nالسبب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AutoBackupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Placeholder for future functionality
        }

        #endregion

        #region Danger Zone

        private void ResetDatabase_Click(object sender, RoutedEventArgs e)
        {
            // First confirmation
            var result1 = MessageBox.Show(
                "هل أنت متأكد من إعادة تعيين قاعدة البيانات؟\n\nسيتم حذف جميع البيانات بشكل نهائي:\n• جميع الموظفين\n• جميع سجلات الحضور\n• جميع الأقسام\n• جميع الورديات\n• جميع الاستثناءات\n• جميع سجلات التدقيق",
                "تأكيد إعادة التعيين",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result1 != MessageBoxResult.Yes) return;

            // Second confirmation
            var result2 = MessageBox.Show(
                "⚠️ تحذير أخير!\n\nهذا الإجراء لا يمكن التراجع عنه!\n\nهل تريد المتابعة؟",
                "تأكيد نهائي",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop,
                MessageBoxResult.No);

            if (result2 != MessageBoxResult.Yes) return;

            try
            {
                this.Cursor = Cursors.Wait;

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Order matters due to foreign key constraints
                    var truncateCommands = new[]
                    {
                        "TRUNCATE TABLE audit_logs RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE admin_department_mappings RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE admin_device_mappings RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE attendance_exceptions RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE employee_exceptions RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE attendance_logs RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE shift_rules RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE user_department_permissions RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE user_device_permissions RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE users RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE shifts RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE departments RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE machines RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE exception_types RESTART IDENTITY CASCADE"
                    };

                    foreach (var sql in truncateCommands)
                    {
                        try
                        {
                            using (var cmd = new NpgsqlCommand(sql, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch
                        {
                            // Continue if table doesn't exist
                        }
                    }

                    // Re-insert default exception types
                    var insertDefaults = @"
                        INSERT INTO exception_types (exception_name) VALUES
                        ('إجازة سنوية'),
                        ('إجازة مرضية'),
                        ('إجازة طارئة'),
                        ('مهمة رسمية'),
                        ('تأخير مبرر')
                        ON CONFLICT DO NOTHING";

                    using (var cmd = new NpgsqlCommand(insertDefaults, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Create default admin user (admin/admin)
                    var insertAdmin = @"
                        INSERT INTO users (badge_number, name, default_dept_id, password, role, can_edit_times)
                        VALUES ('admin', 'admin', 0, 'admin', 'superadmin', true)
                        ON CONFLICT (badge_number) DO NOTHING";

                    using (var cmd = new NpgsqlCommand(insertAdmin, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                this.Cursor = Cursors.Arrow;

                // Log the action
                AuditLogger.Log("DATABASE_RESET", null, null, null, null, "Full database reset performed");

                MessageBox.Show("تم إعادة تعيين قاعدة البيانات بنجاح!\n\nيجب إعادة تشغيل البرنامج.",
                    "تمت العملية", MessageBoxButton.OK, MessageBoxImage.Information);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                MessageBox.Show($"حدث خطأ أثناء إعادة التعيين:\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAttendanceLogs_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "هل أنت متأكد من مسح جميع سجلات الحضور؟\n\nسيتم حذف:\n• جميع سجلات الحضور\n• جميع استثناءات الحضور\n\nمع الإبقاء على بيانات الموظفين والأقسام والورديات.",
                "تأكيد مسح السجلات",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                this.Cursor = Cursors.Wait;

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    var truncateCommands = new[]
                    {
                        "TRUNCATE TABLE attendance_exceptions RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE employee_exceptions RESTART IDENTITY CASCADE",
                        "TRUNCATE TABLE attendance_logs RESTART IDENTITY CASCADE"
                    };

                    foreach (var sql in truncateCommands)
                    {
                        try
                        {
                            using (var cmd = new NpgsqlCommand(sql, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch
                        {
                            // Continue if table doesn't exist
                        }
                    }
                }

                this.Cursor = Cursors.Arrow;

                // Log the action
                AuditLogger.Log("CLEAR_ATTENDANCE_LOGS", null, null, null, null, "All attendance logs cleared");

                MessageBox.Show("تم مسح جميع سجلات الحضور بنجاح!",
                    "تمت العملية", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                MessageBox.Show($"حدث خطأ أثناء مسح السجلات:\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
