using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Npgsql;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class AddEditDepartmentWindow : Window
    {
        public Department DepartmentData { get; private set; }
        public string DepartmentName => DepartmentNameTextBox.Text;
        public int? SelectedHeadUserId => DepartmentHeadComboBox.SelectedValue as int?;

        public AddEditDepartmentWindow(Department department = null)
        {
            InitializeComponent();
            DepartmentData = department ?? new Department();

            LoadEmployees(department?.DeptId);

            if (department != null)
            {
                TitleText.Text = "تعديل قسم";
                DepartmentNameTextBox.Text = department.DeptName;
                if (department.HeadUserId.HasValue)
                {
                    DepartmentHeadComboBox.SelectedValue = department.HeadUserId.Value;
                }
            }
        }

        private void LoadEmployees(int? departmentId = null)
        {
            try
            {
                var employees = new List<User>();
                // Add empty option
                employees.Add(new User { UserId = 0, Name = "-- بدون مدير --" });

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    string sql;
                    if (departmentId.HasValue)
                    {
                        // When editing, show only employees from this department
                        sql = "SELECT user_id, name FROM users WHERE default_dept_id = @deptId ORDER BY name";
                    }
                    else
                    {
                        // When adding new department, show all employees
                        sql = "SELECT user_id, name FROM users ORDER BY name";
                    }

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        if (departmentId.HasValue)
                        {
                            cmd.Parameters.AddWithValue("deptId", departmentId.Value);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                employees.Add(new User
                                {
                                    UserId = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }
                }

                DepartmentHeadComboBox.ItemsSource = employees;
                DepartmentHeadComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading employees: {ex.Message}");
            }
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
            if (string.IsNullOrWhiteSpace(DepartmentNameTextBox.Text))
            {
                MessageBox.Show("اسم القسم لا يمكن أن يكون فارغاً", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.DialogResult = true;
        }
    }
}
