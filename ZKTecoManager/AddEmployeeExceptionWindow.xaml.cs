using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class AddEmployeeExceptionWindow : Window
    {
        private int? _editingExceptionId = null;

        public AddEmployeeExceptionWindow()
        {
            InitializeComponent();
            LoadData();
            ExceptionDatePicker.SelectedDate = DateTime.Today;
        }

        public AddEmployeeExceptionWindow(ExceptionDisplayItem exception) : this()
        {
            _editingExceptionId = exception.ExceptionId;
            TitleText.Text = "تعديل استثناء";

            EmployeeComboBox.SelectedValue = exception.UserId;
            ExceptionTypeComboBox.SelectedValue = exception.ExceptionTypeId;
            ExceptionDatePicker.SelectedDate = exception.ExceptionDate;
            NotesTextBox.Text = exception.Notes;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void LoadData()
        {
            LoadEmployees();
            LoadExceptionTypes();
        }

        private void LoadEmployees()
        {
            var employees = new List<EmployeeItem>();

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT user_id, name, badge_number FROM users ORDER BY name";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            employees.Add(new EmployeeItem
                            {
                                UserId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                BadgeNumber = reader.GetString(2),
                                DisplayText = $"{reader.GetString(1)} ({reader.GetString(2)})"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الموظفين:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            EmployeeComboBox.ItemsSource = employees;
        }

        private void LoadExceptionTypes()
        {
            var types = new List<ExceptionTypeItem>();

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT exception_type_id, exception_name FROM exception_types WHERE is_active = true ORDER BY exception_name";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new ExceptionTypeItem
                            {
                                ExceptionTypeId = reader.GetInt32(0),
                                ExceptionName = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل أنواع الاستثناءات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ExceptionTypeComboBox.ItemsSource = types;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeComboBox.SelectedValue == null)
            {
                MessageBox.Show("الرجاء اختيار الموظف", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ExceptionTypeComboBox.SelectedValue == null)
            {
                MessageBox.Show("الرجاء اختيار نوع الاستثناء", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ExceptionDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("الرجاء اختيار التاريخ", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveButton.IsEnabled = false;

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    if (_editingExceptionId.HasValue)
                    {
                        // Update existing
                        var sql = @"UPDATE employee_exceptions
                                   SET user_id_fk = @userId, exception_type_id_fk = @typeId,
                                       exception_date = @date, notes = @notes, updated_at = NOW()
                                   WHERE exception_id = @id";

                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("userId", (int)EmployeeComboBox.SelectedValue);
                            cmd.Parameters.AddWithValue("typeId", (int)ExceptionTypeComboBox.SelectedValue);
                            cmd.Parameters.AddWithValue("date", ExceptionDatePicker.SelectedDate.Value);
                            cmd.Parameters.AddWithValue("notes", string.IsNullOrEmpty(NotesTextBox.Text) ? DBNull.Value : (object)NotesTextBox.Text);
                            cmd.Parameters.AddWithValue("id", _editingExceptionId.Value);
                            cmd.ExecuteNonQuery();
                        }

                        var empName = (EmployeeComboBox.SelectedItem as EmployeeItem)?.Name ?? "";
                        AuditLogger.Log("UPDATE", "employee_exceptions", _editingExceptionId,
                            null, null, $"تعديل استثناء للموظف: {empName}");
                    }
                    else
                    {
                        // Insert new
                        var sql = @"INSERT INTO employee_exceptions (user_id_fk, exception_type_id_fk, exception_date, notes)
                                   VALUES (@userId, @typeId, @date, @notes)";

                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("userId", (int)EmployeeComboBox.SelectedValue);
                            cmd.Parameters.AddWithValue("typeId", (int)ExceptionTypeComboBox.SelectedValue);
                            cmd.Parameters.AddWithValue("date", ExceptionDatePicker.SelectedDate.Value);
                            cmd.Parameters.AddWithValue("notes", string.IsNullOrEmpty(NotesTextBox.Text) ? DBNull.Value : (object)NotesTextBox.Text);
                            cmd.ExecuteNonQuery();
                        }

                        var empName = (EmployeeComboBox.SelectedItem as EmployeeItem)?.Name ?? "";
                        var typeName = (ExceptionTypeComboBox.SelectedItem as ExceptionTypeItem)?.ExceptionName ?? "";
                        AuditLogger.Log("INSERT", "employee_exceptions", null, null,
                            $"الموظف: {empName}, الاستثناء: {typeName}",
                            $"إضافة استثناء للموظف: {empName}");
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الاستثناء:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                SaveButton.IsEnabled = true;
            }
        }
    }

    public class EmployeeItem
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string BadgeNumber { get; set; }
        public string DisplayText { get; set; }
    }

    public class ExceptionTypeItem
    {
        public int ExceptionTypeId { get; set; }
        public string ExceptionName { get; set; }
    }
}
