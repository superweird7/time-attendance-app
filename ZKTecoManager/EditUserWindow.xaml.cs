using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class EditUserWindow : Window
    {
        private User _userToEdit;

        // Lists to hold data for the dropdowns
        private List<Department> _availableDepartments = new List<Department>();
        private List<Shift> _availableShifts = new List<Shift>();

        public EditUserWindow(User userToEdit)
        {
            InitializeComponent();
            _userToEdit = userToEdit;
            this.Loaded += EditUserWindow_Loaded;
        }

        private void EditUserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial text values
            NameTextBox.Text = _userToEdit.Name;
            BadgeNumberTextBox.Text = _userToEdit.BadgeNumber;

            // Load data for both dropdowns
            LoadDepartments();
            LoadShifts();

            // Pre-select the user's current department
            if (_userToEdit.DefaultDeptId > 0)
            {
                DepartmentComboBox.SelectedValue = _userToEdit.DefaultDeptId;
            }
            else
            {
                DepartmentComboBox.SelectedValue = -1; // Select "Not Assigned"
            }

            // Pre-select the user's current shift
            if (_userToEdit.ShiftId.HasValue)
            {
                ShiftComboBox.SelectedValue = _userToEdit.ShiftId.Value;
            }
            else
            {
                ShiftComboBox.SelectedValue = -1; // Select "Not Assigned"
            }
        }

        #region Data Loading for ComboBoxes
        private void LoadDepartments()
        {
            try
            {
                _availableDepartments.Add(new Department { DeptId = -1, DeptName = "Not Assigned" });

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT dept_id, dept_name FROM departments ORDER BY dept_name";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _availableDepartments.Add(new Department { DeptId = reader.GetInt32(0), DeptName = reader.GetString(1) });
                        }
                    }
                }
                DepartmentComboBox.ItemsSource = _availableDepartments;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load departments: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadShifts()
        {
            try
            {
                _availableShifts.Add(new Shift { ShiftId = -1, ShiftName = "Not Assigned" });

                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT shift_id, shift_name FROM shifts ORDER BY shift_name";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _availableShifts.Add(new Shift { ShiftId = reader.GetInt32(0), ShiftName = reader.GetString(1) });
                        }
                    }
                }
                ShiftComboBox.ItemsSource = _availableShifts;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load shifts: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || string.IsNullOrWhiteSpace(BadgeNumberTextBox.Text))
            {
                MessageBox.Show("Employee Name and Badge Number cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();
                    var sql = "UPDATE users SET name = @name, badge_number = @badge, default_dept_id = @deptId, shift_id = @shiftId WHERE user_id = @id";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("name", NameTextBox.Text.Trim());
                        cmd.Parameters.AddWithValue("badge", BadgeNumberTextBox.Text.Trim());
                        cmd.Parameters.AddWithValue("id", _userToEdit.UserId);

                        // Handle Department assignment
                        if (DepartmentComboBox.SelectedValue != null && (int)DepartmentComboBox.SelectedValue != -1)
                        {
                            cmd.Parameters.AddWithValue("deptId", (int)DepartmentComboBox.SelectedValue);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("deptId", DBNull.Value);
                        }

                        // Handle Shift assignment
                        if (ShiftComboBox.SelectedValue != null && (int)ShiftComboBox.SelectedValue != -1)
                        {
                            cmd.Parameters.AddWithValue("shiftId", (int)ShiftComboBox.SelectedValue);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("shiftId", DBNull.Value);
                        }

                        cmd.ExecuteNonQuery();
                    }
                }
                // Build old and new values for audit log with field-level tracking
                var oldDeptName = _availableDepartments.Find(d => d.DeptId == _userToEdit.DefaultDeptId)?.DeptName ?? "غير محدد";
                var newDeptName = _availableDepartments.Find(d => d.DeptId == (int)(DepartmentComboBox.SelectedValue ?? -1))?.DeptName ?? "غير محدد";
                var oldShiftName = _availableShifts.Find(s => s.ShiftId == (_userToEdit.ShiftId ?? -1))?.ShiftName ?? "غير محدد";
                var newShiftName = _availableShifts.Find(s => s.ShiftId == (int)(ShiftComboBox.SelectedValue ?? -1))?.ShiftName ?? "غير محدد";

                // Track individual field changes
                var changes = new List<string>();
                if (NameTextBox.Text.Trim() != _userToEdit.Name)
                    changes.Add($"Name: '{_userToEdit.Name}' → '{NameTextBox.Text.Trim()}'");
                if (BadgeNumberTextBox.Text.Trim() != _userToEdit.BadgeNumber)
                    changes.Add($"Badge: '{_userToEdit.BadgeNumber}' → '{BadgeNumberTextBox.Text.Trim()}'");
                if ((int)(DepartmentComboBox.SelectedValue ?? -1) != (_userToEdit.DefaultDeptId == 0 ? -1 : _userToEdit.DefaultDeptId))
                    changes.Add($"Dept: '{oldDeptName}' → '{newDeptName}'");
                if ((int)(ShiftComboBox.SelectedValue ?? -1) != (_userToEdit.ShiftId ?? -1))
                    changes.Add($"Shift: '{oldShiftName}' → '{newShiftName}'");

                var changesSummary = changes.Count > 0 ? string.Join(", ", changes) : "No changes";

                var oldValue = $"Name: {_userToEdit.Name}, Badge: {_userToEdit.BadgeNumber}, Dept: {oldDeptName}, Shift: {oldShiftName}";
                var newValue = $"Name: {NameTextBox.Text.Trim()}, Badge: {BadgeNumberTextBox.Text.Trim()}, Dept: {newDeptName}, Shift: {newShiftName}";

                AuditLogger.Log("UPDATE", "users", _userToEdit.UserId, oldValue, newValue,
                    $"Employee changes: {_userToEdit.Name} - {changesSummary}");

                MessageBox.Show("Employee updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update user:\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
        private void CancelButton_Click(object sender, RoutedEventArgs e) { this.DialogResult = false; this.Close(); }
    }
}

