using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class AdminPanelWindow : Window
    {
        private List<User> _allUsers;
        private List<SelectableItem> allDepartments;
        private List<SelectableItem> _allDevices;

        // New properties for dept admin management
        private string _currentUserRole;
        private bool _isManagingDeptAdmins = false;

        public AdminPanelWindow(string currentUserRole)
        {
            InitializeComponent();
            _currentUserRole = currentUserRole;
            this.Loaded += Window_Loaded;
        }

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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadInitialData();

            // Show management button and activity log only for superadmin
            if (_currentUserRole == "superadmin")
            {
                ManageDeptAdminsButton.Visibility = Visibility.Visible;
                ActivityLogButton.Visibility = Visibility.Visible;
            }
        }

        private void ActivityLogButton_Click(object sender, RoutedEventArgs e)
        {
            var activityLogWindow = new UserActivityLogWindow();
            activityLogWindow.Owner = this;
            activityLogWindow.ShowDialog();
        }

        private void LoadInitialData()
        {
            try
            {
                _allUsers = GetAllUsers();
                allDepartments = GetAllDepartments();
                _allDevices = GetAllDevices();

                UsersListBox.ItemsSource = _allUsers;
                DepartmentsListBox.ItemsSource = allDepartments;
                DevicesListBox.ItemsSource = _allDevices;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"فشل تحميل البيانات الأولية: {ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UserSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allUsers == null) return;

            string searchText = UserSearchTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                if (_isManagingDeptAdmins)
                {
                    var deptAdmins = _allUsers.Where(u => u.Role == "deptadmin").ToList();
                    UsersListBox.ItemsSource = deptAdmins;
                }
                else
                {
                    UsersListBox.ItemsSource = _allUsers;
                }
            }
            else
            {
                var sourceList = _isManagingDeptAdmins
                    ? _allUsers.Where(u => u.Role == "deptadmin").ToList()
                    : _allUsers;

                var filteredUsers = sourceList.Where(u => u.Name != null && u.Name.ToLower().Contains(searchText)).ToList();
                UsersListBox.ItemsSource = filteredUsers;
            }
        }

        private void UsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            allDepartments.ForEach(d => d.IsSelected = false);
            _allDevices.ForEach(d => d.IsSelected = false);

            if (UsersListBox.SelectedItem is User selectedUser)
            {
                DetailsPanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                SelectedUserName.Text = selectedUser.Name;

                RoleComboBox.SelectedIndex = selectedUser.Role == "superadmin" ? 0 : 1;

                // Set SystemAccessType ComboBox
                SystemAccessTypeComboBox.SelectedIndex = selectedUser.SystemAccessType == "leave_only" ? 1 : 0;

                if (selectedUser.Role == "deptadmin")
                {
                    PermissionsPanel.Visibility = Visibility.Visible;
                    LoadPermissionsForUser(selectedUser.UserId);

                    // Show management options ONLY if in management mode AND user is deptadmin
                    if (_isManagingDeptAdmins)
                    {
                        ShowDeptAdminManagementOptions();
                    }
                    else
                    {
                        HideDeptAdminManagementOptions();
                    }
                }
                else
                {
                    PermissionsPanel.Visibility = Visibility.Collapsed;
                    HideDeptAdminManagementOptions();
                }
            }
            else
            {
                DetailsPanel.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                HideDeptAdminManagementOptions();
            }
        }


        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoleComboBox.SelectedIndex == 1) // deptadmin
            {
                PermissionsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PermissionsPanel.Visibility = Visibility.Collapsed;
                HideDeptAdminManagementOptions();
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListBox.SelectedItem is not User selectedUser)
            {
                System.Windows.MessageBox.Show("الرجاء تحديد مستخدم أولاً", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newRole = RoleComboBox.SelectedIndex == 0 ? "superadmin" : "deptadmin";
            string selectedAccessType = ((ComboBoxItem)SystemAccessTypeComboBox.SelectedItem)?.Tag?.ToString() ?? "full_access";

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            var updateRoleSql = "UPDATE users SET role = @role, system_access_type = @accessType WHERE user_id = @userId";
                            using (var cmd = new NpgsqlCommand(updateRoleSql, conn))
                            {
                                cmd.Parameters.AddWithValue("role", newRole);
                                cmd.Parameters.AddWithValue("accessType", selectedAccessType);
                                cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                                cmd.ExecuteNonQuery();
                            }

                            var deletePermsSql = "DELETE FROM admin_department_mappings WHERE user_id_fk = @userId; DELETE FROM admin_device_mappings WHERE user_id_fk = @userId;";
                            using (var cmd = new NpgsqlCommand(deletePermsSql, conn))
                            {
                                cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                                cmd.ExecuteNonQuery();
                            }

                            if (newRole == "deptadmin")
                            {
                                foreach (var dept in allDepartments.Where(d => d.IsSelected))
                                {
                                    var sql = "INSERT INTO admin_department_mappings (user_id_fk, department_id_fk) VALUES (@userId, @deptId)";
                                    using (var cmd = new NpgsqlCommand(sql, conn))
                                    {
                                        cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                                        cmd.Parameters.AddWithValue("deptId", dept.Id);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                foreach (var device in _allDevices.Where(d => d.IsSelected))
                                {
                                    var sql = "INSERT INTO admin_device_mappings (user_id_fk, device_id_fk) VALUES (@userId, @devId)";
                                    using (var cmd = new NpgsqlCommand(sql, conn))
                                    {
                                        cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                                        cmd.Parameters.AddWithValue("devId", device.Id);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }

                // Log the permission change
                var selectedDepts = string.Join(", ", allDepartments.Where(d => d.IsSelected).Select(d => d.Name).Take(3));
                if (allDepartments.Count(d => d.IsSelected) > 3)
                    selectedDepts += " ...";
                var selectedDevs = string.Join(", ", _allDevices.Where(d => d.IsSelected).Select(d => d.Name).Take(3));
                if (_allDevices.Count(d => d.IsSelected) > 3)
                    selectedDevs += " ...";

                AuditLogger.Log("UPDATE", "users", selectedUser.UserId,
                    $"الدور: {selectedUser.Role}",
                    $"الدور: {newRole}, الأقسام: [{selectedDepts}], الأجهزة: [{selectedDevs}]",
                    $"تعديل صلاحيات المستخدم: {selectedUser.Name}");

                System.Windows.MessageBox.Show("تم حفظ صلاحيات المستخدم بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                int selectedIndex = UsersListBox.SelectedIndex;
                LoadInitialData();

                if (_isManagingDeptAdmins)
                {
                    var deptAdmins = _allUsers.Where(u => u.Role == "deptadmin").ToList();
                    UsersListBox.ItemsSource = deptAdmins;
                }

                UsersListBox.SelectedIndex = selectedIndex;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"فشل حفظ الصلاحيات: {ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Department Admin Management

        private void ManageDeptAdminsButton_Click(object sender, RoutedEventArgs e)
        {
            _isManagingDeptAdmins = !_isManagingDeptAdmins;

            if (_isManagingDeptAdmins)
            {
                // Filter to show only deptadmins
                var deptAdmins = _allUsers.Where(u => u.Role == "deptadmin").ToList();
                UsersListBox.ItemsSource = deptAdmins;
                ManageDeptAdminsButton.Content = "📋 عرض جميع المستخدمين";

                // Clear selection
                UsersListBox.SelectedIndex = -1;
                DetailsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Show all users
                UsersListBox.ItemsSource = _allUsers;
                ManageDeptAdminsButton.Content = "🔧 إدارة مدراء الأقسام";

                // Clear selection
                UsersListBox.SelectedIndex = -1;
                DetailsPanel.Visibility = Visibility.Collapsed;
            }
        }


        private void ShowDeptAdminManagementOptions()
        {
            // Find the ScrollViewer's StackPanel - DetailsPanel is now a Border directly
            var scrollViewer = DetailsPanel.Child as ScrollViewer;
            var mainStack = scrollViewer?.Content as StackPanel;

            if (mainStack == null) return;

            // Check if management panel already exists
            var existingPanel = mainStack.Children.OfType<StackPanel>()
                .FirstOrDefault(sp => sp.Name == "DeptAdminManagementPanel");

            if (existingPanel != null)
            {
                existingPanel.Visibility = Visibility.Visible;
                return;
            }

            // Create management panel
            var managementPanel = new StackPanel
            {
                Name = "DeptAdminManagementPanel",
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            // Change Password Button
            var changePasswordBtn = new System.Windows.Controls.Button
            {
                Content = "🔑 تغيير كلمة المرور",
                Height = 45,
                Padding = new Thickness(20, 0, 20, 0),
                Margin = new Thickness(10, 0, 10, 0),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            // Apply gradient background
            var changePasswordGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            changePasswordGradient.GradientStops.Add(new GradientStop(Color.FromRgb(59, 130, 246), 0));
            changePasswordGradient.GradientStops.Add(new GradientStop(Color.FromRgb(37, 99, 235), 1));
            changePasswordBtn.Background = changePasswordGradient;

            // Apply template for rounded corners
            var changePasswordTemplate = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var changePasswordBorder = new FrameworkElementFactory(typeof(Border));
            changePasswordBorder.Name = "border";
            changePasswordBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            changePasswordBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));

            var changePasswordPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            changePasswordPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            changePasswordPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            changePasswordBorder.AppendChild(changePasswordPresenter);
            changePasswordTemplate.VisualTree = changePasswordBorder;
            changePasswordBtn.Template = changePasswordTemplate;

            changePasswordBtn.Click += ChangePasswordButton_Click;

            // Delete Button
            var deleteBtn = new System.Windows.Controls.Button
            {
                Content = "🗑️ حذف المستخدم",
                Height = 45,
                Padding = new Thickness(20, 0, 20, 0),
                Margin = new Thickness(10, 0, 0, 0),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            // Apply gradient background
            var deleteGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            deleteGradient.GradientStops.Add(new GradientStop(Color.FromRgb(239, 68, 68), 0));
            deleteGradient.GradientStops.Add(new GradientStop(Color.FromRgb(220, 38, 38), 1));
            deleteBtn.Background = deleteGradient;

            // Apply template for rounded corners
            var deleteTemplate = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var deleteBorder = new FrameworkElementFactory(typeof(Border));
            deleteBorder.Name = "border";
            deleteBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            deleteBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));

            var deletePresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            deletePresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            deletePresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            deleteBorder.AppendChild(deletePresenter);
            deleteTemplate.VisualTree = deleteBorder;
            deleteBtn.Template = deleteTemplate;

            deleteBtn.Click += DeleteDeptAdminButton_Click;

            managementPanel.Children.Add(changePasswordBtn);
            managementPanel.Children.Add(deleteBtn);

            // Insert before Save button (last item in stack)
            mainStack.Children.Insert(mainStack.Children.Count - 1, managementPanel);

            // Register the name for FindName to work
            this.RegisterName("DeptAdminManagementPanel", managementPanel);
        }

        private void HideDeptAdminManagementOptions()
        {
            // DetailsPanel is now a Border directly
            var scrollViewer = DetailsPanel.Child as ScrollViewer;
            var mainStack = scrollViewer?.Content as StackPanel;

            if (mainStack == null) return;

            var panel = mainStack.Children.OfType<StackPanel>()
                .FirstOrDefault(sp => sp.Name == "DeptAdminManagementPanel");

            if (panel != null)
            {
                panel.Visibility = Visibility.Collapsed;
            }
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListBox.SelectedItem is not User selectedUser)
            {
                System.Windows.MessageBox.Show("الرجاء تحديد مستخدم أولاً", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create password dialog
            var passwordDialog = new Window
            {
                Title = "تغيير كلمة المرور",
                Width = 400,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = Brushes.White
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"تغيير كلمة المرور لـ: {selectedUser.Name}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20),
                TextAlignment = TextAlignment.Right
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "كلمة المرور الجديدة:",
                Margin = new Thickness(0, 0, 0, 5),
                TextAlignment = TextAlignment.Right,
                FontSize = 13
            });

            var passwordBox = new PasswordBox
            {
                Height = 35,
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(8)
            };
            stackPanel.Children.Add(passwordBox);

            stackPanel.Children.Add(new TextBlock
            {
                Text = "تأكيد كلمة المرور:",
                Margin = new Thickness(0, 10, 0, 5),
                TextAlignment = TextAlignment.Right,
                FontSize = 13
            });

            var confirmPasswordBox = new PasswordBox
            {
                Height = 35,
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(8)
            };
            stackPanel.Children.Add(confirmPasswordBox);

            var buttonPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var saveButton = new System.Windows.Controls.Button
            {
                Content = "حفظ",
                Width = 100,
                Height = 35,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            saveButton.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(passwordBox.Password))
                {
                    System.Windows.MessageBox.Show("الرجاء إدخال كلمة المرور", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (passwordBox.Password != confirmPasswordBox.Password)
                {
                    System.Windows.MessageBox.Show("كلمات المرور غير متطابقة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (passwordBox.Password.Length < 6)
                {
                    System.Windows.MessageBox.Show("كلمة المرور يجب أن تكون 6 أحرف على الأقل", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    // Use plain text password (no hashing)
                    string plainPassword = passwordBox.Password;

                    using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();
                        var sql = "UPDATE users SET password = @password WHERE user_id = @userId";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("password", plainPassword);  // ✅ Changed
                            cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Log the password change
                    AuditLogger.Log("UPDATE", "users", selectedUser.UserId, null, null,
                        $"تغيير كلمة مرور المستخدم: {selectedUser.Name} بواسطة المدير");

                    System.Windows.MessageBox.Show("تم تغيير كلمة المرور بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    passwordDialog.Close();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"فشل تغيير كلمة المرور: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "إلغاء",
                Width = 100,
                Height = 35,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            cancelButton.Click += (s, args) => passwordDialog.Close();

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            passwordDialog.Content = stackPanel;
            passwordDialog.ShowDialog();
        }

        private void DeleteDeptAdminButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListBox.SelectedItem is not User selectedUser)
            {
                System.Windows.MessageBox.Show("الرجاء تحديد مستخدم أولاً", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirmation dialog
            // CORRECT - WPF version
            var result = System.Windows.MessageBox.Show(
                $"هل أنت متأكد من حذف المستخدم '{selectedUser.Name}'؟\n\nسيتم حذف جميع الصلاحيات المرتبطة بهذا المستخدم.",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);  // <-- THIS IS CORRECT

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // Delete permissions first (foreign key constraints)
                            var deletePermsSql = @"
                                DELETE FROM admin_department_mappings WHERE user_id_fk = @userId;
                                DELETE FROM admin_device_mappings WHERE user_id_fk = @userId;";
                            using (var cmd = new NpgsqlCommand(deletePermsSql, conn))
                            {
                                cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                                cmd.ExecuteNonQuery();
                            }

                            // Delete user
                            var deleteUserSql = "DELETE FROM users WHERE user_id = @userId";
                            using (var cmd = new NpgsqlCommand(deleteUserSql, conn))
                            {
                                cmd.Parameters.AddWithValue("userId", selectedUser.UserId);
                                cmd.ExecuteNonQuery();
                            }

                            trans.Commit();

                            // Log the deletion
                            AuditLogger.Log("DELETE", "users", selectedUser.UserId,
                                $"الاسم: {selectedUser.Name}, الدور: {selectedUser.Role}",
                                null,
                                $"حذف المستخدم: {selectedUser.Name}");

                            System.Windows.MessageBox.Show("تم حذف المستخدم بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Refresh the list
                            LoadInitialData();
                            if (_isManagingDeptAdmins)
                            {
                                var deptAdmins = _allUsers.Where(u => u.Role == "deptadmin").ToList();
                                UsersListBox.ItemsSource = deptAdmins;
                            }

                            DetailsPanel.Visibility = Visibility.Collapsed;
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"فشل حذف المستخدم: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Database Methods

        private List<User> GetAllUsers()
        {
            var users = new List<User>();
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sql = "SELECT user_id, name, role, system_access_type FROM users ORDER BY name";
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new User
                        {
                            UserId = reader.GetInt32(0),
                            Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Role = reader.GetString(2),
                            SystemAccessType = reader.IsDBNull(3) ? "full_access" : reader.GetString(3)
                        });
                    }
                }
            }
            return users;
        }

        private List<SelectableItem> GetAllDepartments()
        {
            var items = new List<SelectableItem>();
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sql = "SELECT dept_id, dept_name FROM departments ORDER BY dept_name";
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new SelectableItem { Id = reader.GetInt32(0), Name = reader.GetString(1) });
                    }
                }
            }
            return items;
        }

        private List<SelectableItem> GetAllDevices()
        {
            var items = new List<SelectableItem>();
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sql = "SELECT id, machine_alias FROM machines ORDER BY machine_alias";
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new SelectableItem { Id = reader.GetInt32(0), Name = reader.GetString(1) });
                    }
                }
            }
            return items;
        }

        private void LoadPermissionsForUser(int userId)
        {
            var permittedDeptIds = new HashSet<int>();
            var permittedDevIds = new HashSet<int>();

            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var deptSql = "SELECT department_id_fk FROM admin_department_mappings WHERE user_id_fk = @userId";
                using (var cmd = new NpgsqlCommand(deptSql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) permittedDeptIds.Add(reader.GetInt32(0));
                    }
                }

                var devSql = "SELECT device_id_fk FROM admin_device_mappings WHERE user_id_fk = @userId";
                using (var cmd = new NpgsqlCommand(devSql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) permittedDevIds.Add(reader.GetInt32(0));
                    }
                }
            }

            allDepartments.ForEach(d => d.IsSelected = permittedDeptIds.Contains(d.Id));
            _allDevices.ForEach(d => d.IsSelected = permittedDevIds.Contains(d.Id));
        }

        #endregion

        #region Helper Methods

        // Simple password hashing using SHA256
        // IMPORTANT: For production, use BCrypt.Net-Next library instead
        // Install via: Install-Package BCrypt.Net-Next
        // Then use: BCrypt.HashPassword(password) and BCrypt.Verify(password, hash)
        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        #endregion
    }
}
