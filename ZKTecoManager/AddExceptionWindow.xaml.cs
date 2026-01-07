using Npgsql;
using System;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class AddExceptionWindow : Window
    {

        public AddExceptionWindow()
        {
            InitializeComponent();
            ExceptionNameTextBox.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string exceptionName = ExceptionNameTextBox.Text.Trim();

            // Validate input
            if (string.IsNullOrWhiteSpace(exceptionName))
            {
                MessageBox.Show("الرجاء إدخال اسم الاستثناء.", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                ExceptionNameTextBox.Focus();
                return;
            }

            // Disable button to prevent double-clicks
            SaveButton.IsEnabled = false;

            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                    {
                        connection.Open();

                        // Check for duplicate exception name
                        var checkSql = "SELECT COUNT(*) FROM exception_types WHERE LOWER(exception_name) = LOWER(@name)";
                        using (var checkCmd = new NpgsqlCommand(checkSql, connection))
                        {
                            checkCmd.Parameters.AddWithValue("name", exceptionName);
                            int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (count > 0)
                            {
                                throw new InvalidOperationException($"الاستثناء '{exceptionName}' موجود بالفعل.");
                            }
                        }

                        // Insert new exception type
                        var sql = "INSERT INTO exception_types (exception_name) VALUES (@name)";
                        using (var cmd = new NpgsqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("name", exceptionName);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });

                MessageBox.Show("تم إضافة نوع الاستثناء بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "استثناء مكرر", MessageBoxButton.OK, MessageBoxImage.Warning);
                SaveButton.IsEnabled = true;
                ExceptionNameTextBox.Focus();
                ExceptionNameTextBox.SelectAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل في حفظ الاستثناء:\n\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                SaveButton.IsEnabled = true;
            }
        }
    }
}
