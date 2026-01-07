using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class BulkAddUsersWindow : Window
    {
        private List<Department> _departments;
        private ObservableCollection<UserToAdd> _usersToAdd;

        public BulkAddUsersWindow(List<string> badgeNumbers)
        {
            InitializeComponent();
            LoadDepartments();

            _usersToAdd = new ObservableCollection<UserToAdd>(
                badgeNumbers.Select(b => new UserToAdd { BadgeNumber = b, Name = "" })
            );

            UsersItemsControl.ItemsSource = _usersToAdd;
        }

        private void LoadDepartments()
        {
            _departments = new List<Department>();

            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sql = "SELECT dept_id, dept_name FROM departments ORDER BY dept_name";
                using (var cmd = new NpgsqlCommand(sql, conn))
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

            DefaultDepartmentComboBox.ItemsSource = _departments;
            DefaultDepartmentComboBox.DisplayMemberPath = "DeptName";
            DefaultDepartmentComboBox.SelectedValuePath = "DeptId";

            if (_departments.Count > 0)
                DefaultDepartmentComboBox.SelectedIndex = 0;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DefaultDepartmentComboBox.SelectedValue == null)
            {
                MessageBox.Show("الرجاء اختيار قسم", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var deptId = (int)DefaultDepartmentComboBox.SelectedValue;
            int addedCount = 0;

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // Use transaction for atomic bulk insert - all or nothing
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (var user in _usersToAdd)
                            {
                                var name = string.IsNullOrWhiteSpace(user.Name) ? $"User_{user.BadgeNumber}" : user.Name;

                                var sql = "INSERT INTO users (name, badge_number, default_dept_id) VALUES (@name, @badge, @deptId)";
                                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("name", name);
                                    cmd.Parameters.AddWithValue("badge", user.BadgeNumber);
                                    cmd.Parameters.AddWithValue("deptId", deptId);
                                    cmd.ExecuteNonQuery();
                                    addedCount++;
                                }
                            }

                            transaction.Commit();

                            // Log the bulk addition
                            var deptName = _departments.Find(d => d.DeptId == deptId)?.DeptName ?? "";
                            var badgeNumbers = string.Join(", ", _usersToAdd.Take(5).Select(u => u.BadgeNumber));
                            if (_usersToAdd.Count > 5)
                                badgeNumbers += $" و {_usersToAdd.Count - 5} آخرين";

                            AuditLogger.Log("INSERT", "users", null, null,
                                $"عدد: {addedCount}, القسم: {deptName}",
                                $"اضافة {addedCount} موظف جديد بشكل جماعي (أرقام البطاقات: {badgeNumbers})");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الإضافة:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class UserToAdd : INotifyPropertyChanged
    {
        private string _name;
        public string BadgeNumber { get; set; }
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
