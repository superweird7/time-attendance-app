using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class AddUserWindow : Window
    {
        private List<Department> _departments = new List<Department>();

        public AddUserWindow()
        {
            InitializeComponent();
            this.Loaded += AddUserWindow_Loaded;
        }

        private void AddUserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDepartments();
        }

        private void LoadDepartments()
        {
            try
            {
                _departments.Clear();

                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();
                    var sql = "SELECT dept_id, dept_name FROM departments ORDER BY dept_name";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _departments.Add(new Department
                            {
                                DeptId = reader.GetInt32(0),
                                DeptName = reader.GetString(1)
                            });
                        }
                    }
                }

                DepartmentComboBox.ItemsSource = _departments;
                DepartmentComboBox.DisplayMemberPath = "DeptName";
                DepartmentComboBox.SelectedValuePath = "DeptId";

                if (_departments.Count > 0)
                {
                    DepartmentComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load departments: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Make window draggable
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Cancel button
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter an employee name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(BadgeNumberTextBox.Text))
            {
                MessageBox.Show("Please enter a badge number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                BadgeNumberTextBox.Focus();
                return;
            }

            if (DepartmentComboBox.SelectedValue == null)
            {
                MessageBox.Show("Please select a department.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DepartmentComboBox.Focus();
                return;
            }

            try
            {
                // Normalize badge number - remove leading zeros for consistency with device downloads
                var normalizedBadge = BadgeNumberTextBox.Text.Trim().TrimStart('0');
                if (string.IsNullOrEmpty(normalizedBadge)) normalizedBadge = "0";

                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();
                    var sql = "INSERT INTO users (name, badge_number, default_dept_id) VALUES (@name, @badge, @deptId)";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("name", NameTextBox.Text.Trim());
                        cmd.Parameters.AddWithValue("badge", normalizedBadge);
                        cmd.Parameters.AddWithValue("deptId", (int)DepartmentComboBox.SelectedValue);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Log the addition
                AuditLogger.Log("INSERT", "users", null, null,
                    $"الاسم: {NameTextBox.Text.Trim()}, رقم البطاقة: {normalizedBadge}",
                    $"اضافة موظف جديد: {NameTextBox.Text.Trim()} (رقم البطاقة: {normalizedBadge})");

                MessageBox.Show("Employee added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save employee:\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
        }
    }
}
