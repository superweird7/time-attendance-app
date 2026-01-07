using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class ManageExceptionsWindow : Window
    {
        public ManageExceptionsWindow()
        {
            InitializeComponent();
            this.Loaded += Window_Loaded;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void RefreshList()
        {
            var types = new List<ExceptionTypeDisplay>();

            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT exception_type_id, exception_name, is_active FROM exception_types ORDER BY exception_name";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new ExceptionTypeDisplay
                            {
                                ExceptionTypeId = reader.GetInt32(0),
                                ExceptionName = reader.GetString(1),
                                IsActive = reader.GetBoolean(2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الاستثناءات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ExceptionsListBox.ItemsSource = types;
        }

        private void AddException_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddExceptionWindow { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                RefreshList();
            }
        }

        private void EditException_Click(object sender, RoutedEventArgs e)
        {
            if (ExceptionsListBox.SelectedItem == null)
            {
                MessageBox.Show("الرجاء اختيار استثناء من القائمة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = (ExceptionTypeDisplay)ExceptionsListBox.SelectedItem;

            // Show edit dialog
            var dialog = new EditExceptionTypeDialog(selected.ExceptionName) { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ExceptionName))
            {
                var newName = dialog.ExceptionName.Trim();
                if (newName != selected.ExceptionName)
                {
                    try
                    {
                        using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                        {
                            conn.Open();
                            var sql = "UPDATE exception_types SET exception_name = @name WHERE exception_type_id = @id";

                            using (var cmd = new NpgsqlCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("name", newName);
                                cmd.Parameters.AddWithValue("id", selected.ExceptionTypeId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        AuditLogger.Log("UPDATE", "exception_types", selected.ExceptionTypeId,
                            selected.ExceptionName, newName, $"تعديل نوع استثناء: {selected.ExceptionName} -> {newName}");

                        RefreshList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"خطأ في تعديل الاستثناء:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteException_Click(object sender, RoutedEventArgs e)
        {
            if (ExceptionsListBox.SelectedItem == null)
            {
                MessageBox.Show("الرجاء اختيار استثناء من القائمة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = (ExceptionTypeDisplay)ExceptionsListBox.SelectedItem;

            var result = MessageBox.Show(
                $"هل أنت متأكد من حذف الاستثناء '{selected.ExceptionName}'؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();

                        // Check if used
                        var checkSql = "SELECT COUNT(*) FROM employee_exceptions WHERE exception_type_id_fk = @id";
                        using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                        {
                            checkCmd.Parameters.AddWithValue("id", selected.ExceptionTypeId);
                            var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (count > 0)
                            {
                                MessageBox.Show(
                                    $"لا يمكن حذف هذا الاستثناء لأنه مستخدم في {count} سجل.",
                                    "لا يمكن الحذف",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                return;
                            }
                        }

                        var sql = "DELETE FROM exception_types WHERE exception_type_id = @id";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("id", selected.ExceptionTypeId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditLogger.Log("DELETE", "exception_types", selected.ExceptionTypeId,
                        selected.ExceptionName, null, $"حذف نوع استثناء: {selected.ExceptionName}");

                    MessageBox.Show("تم حذف الاستثناء بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في حذف الاستثناء:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class ExceptionTypeDisplay
    {
        public int ExceptionTypeId { get; set; }
        public string ExceptionName { get; set; }
        public bool IsActive { get; set; }
    }
}
