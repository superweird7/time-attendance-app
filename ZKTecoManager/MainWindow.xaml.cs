using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Services;

namespace ZKTecoManager
{
    public partial class MainWindow : Window
    {
        public ICommand OpenShortcutsCommand { get; }

        public MainWindow()
        {
            OpenShortcutsCommand = new RelayCommand(_ => OpenKeyboardShortcuts());
            DataContext = this;

            InitializeComponent();
            this.PreviewKeyDown += MainWindow_KeyDown;

            // Subscribe to font size changes
            FontSizeManager.FontSizeChanged += (s, e) => UpdateFontSizeDisplay();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Font size shortcuts: Ctrl++ and Ctrl+-
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.OemPlus || e.Key == Key.Add)
                {
                    IncreaseFontSize_Click(null, null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                {
                    DecreaseFontSize_Click(null, null);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                {
                    // Ctrl+0 to reset font size
                    FontSizeManager.Reset();
                    e.Handled = true;
                    return;
                }
            }

            switch (e.Key)
            {
                case Key.F1:
                    DashboardButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F2:
                    EmployeesButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F3:
                    DepartmentsButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F4:
                    ShiftsButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F5:
                    DevicesButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F6:
                    ReportsButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F7:
                    SyncButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F8:
                    ChangesLogButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F9:
                    SystemHealthButton_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.F10:
                    OpenKeyboardShortcuts();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    QuitButton_Click(null, null);
                    e.Handled = true;
                    break;
            }
        }

        private void OpenKeyboardShortcuts()
        {
            var shortcutsWindow = new KeyboardShortcutsWindow() { Owner = this };
            shortcutsWindow.ShowDialog();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Display welcome message
            WelcomeTextBlock.Text = $"مرحباً، {CurrentUser.Name} • Welcome, {CurrentUser.Name}";
            UserNameTextBlock.Text = CurrentUser.Name;

            // Show admin panel button only for superadmin
            if (CurrentUser.Role == "superadmin")
            {
                AdminPanelButton.Visibility = Visibility.Visible;
            }

            // Show leave management button for superadmin
            if (CurrentUser.CanAccessLeaveManagement && !CurrentUser.IsLeaveAdmin)
            {
                LeaveManagementButton.Visibility = Visibility.Visible;
            }

            // Configure feature visibility based on system access type
            if (CurrentUser.SystemAccessType == "leave_only")
            {
                // HIDE attendance-related features for leave-only users
                DashboardButton.Visibility = Visibility.Collapsed;
                DevicesButton.Visibility = Visibility.Collapsed;
                ReportsButton.Visibility = Visibility.Collapsed;
                SyncButton.Visibility = Visibility.Collapsed;
                ChangesLogButton.Visibility = Visibility.Collapsed;
                SystemHealthButton.Visibility = Visibility.Collapsed;
            }

            // Initialize font size display and apply current scale
            UpdateFontSizeDisplay();
            FontSizeManager.ApplyToWindow(this);

            // Log the login
            AuditLogger.Log("LOGIN", null, null, null, null, $"User logged in: {CurrentUser.Name}");
        }

        #region Font Size Controls

        private void IncreaseFontSize_Click(object sender, RoutedEventArgs e)
        {
            FontSizeManager.Increase();
        }

        private void DecreaseFontSize_Click(object sender, RoutedEventArgs e)
        {
            FontSizeManager.Decrease();
        }

        private void UpdateFontSizeDisplay()
        {
            FontSizeDisplay.Text = FontSizeManager.GetDisplayPercentage();
        }

        #endregion

        #region Window Controls

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "هل أنت متأكد من الخروج من البرنامج؟\nAre you sure you want to exit?",
                "تأكيد الخروج - Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Log logout
                AuditLogger.Log("LOGOUT", null, null, null, null, $"User logged out: {CurrentUser.Name}");

                // Clear current user
                CurrentUser.Clear();

                // Close application
                Application.Current.Shutdown();
            }
        }

        #endregion

        #region Navigation Buttons

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dashboardWindow = new DashboardWindow { Owner = this };
                dashboardWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح لوحة المتابعة:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EmployeesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var employeesWindow = new EmployeesWindow { Owner = this };
                employeesWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة الموظفين:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DepartmentsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var deptWindow = new DepartmentsWindow { Owner = this };
                deptWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة الأقسام:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShiftsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var shiftsWindow = new ShiftsWindow { Owner = this };
                shiftsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة الورديات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var devicesWindow = new DevicesWindow { Owner = this };
                devicesWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة الأجهزة:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportsWindow = new ReportsWindow { Owner = this };
                reportsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة التقارير:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var aboutWindow = new AboutWindow { Owner = this };
                aboutWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة حول:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var backupWindow = new BackupWindow { Owner = this };
                backupWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة النسخ الاحتياطي:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageExceptionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var manageExceptionsWindow = new ManageExceptionsWindow { Owner = this };
                manageExceptionsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة إدارة الاستثناءات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var syncWindow = new SyncDashboardWindow { Owner = this };
                syncWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة المزامنة:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangesLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var changesWindow = new ChangesLogWindow { Owner = this };
                changesWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح سجل التغييرات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SystemHealthButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var healthWindow = new SystemHealthWindow { Owner = this };
                healthWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة صحة النظام:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LeaveManagementButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var leaveWindow = new LeaveManagementWindow { Owner = this };
                leaveWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة إدارة الإجازات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Header Buttons

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var changePasswordWindow = new ChangePasswordWindow { Owner = this };
                changePasswordWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة تغيير كلمة المرور:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        // ✅ FIXED: Admin Panel Button
        private void AdminPanelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pass the current user's role to AdminPanelWindow
                var adminWindow = new AdminPanelWindow(CurrentUser.Role) { Owner = this };
                adminWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح لوحة المدير:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WebDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = WebDashboardService.Instance;

                if (service.IsRunning)
                {
                    // Stop the server
                    service.Stop();
                    WebDashboardButtonText.Text = "Web";
                    WebDashboardButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    MessageBox.Show("تم إيقاف لوحة المتابعة الويب", "Web Dashboard",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Start the server
                    if (service.Start(8080))
                    {
                        WebDashboardButtonText.Text = "● Live";
                        WebDashboardButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));

                        var result = MessageBox.Show(
                            $"تم تشغيل لوحة المتابعة الويب!\n\n" +
                            $"الرابط: {service.DashboardUrl}\n\n" +
                            $"يمكن للمدراء الدخول من أي متصفح على نفس الشبكة.\n\n" +
                            $"هل تريد فتح اللوحة الآن؟",
                            "Web Dashboard",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = service.DashboardUrl,
                                UseShellExecute = true
                            });
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "فشل تشغيل لوحة المتابعة.\n\n" +
                            "قد تحتاج لتشغيل البرنامج كمسؤول (Administrator)\n" +
                            "أو قد يكون المنفذ 8080 مستخدم من برنامج آخر.",
                            "خطأ",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
