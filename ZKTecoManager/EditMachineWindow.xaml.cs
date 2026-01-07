using Npgsql;
using System;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class EditMachineWindow : Window
    {
        private Machine _machineToEdit;

        public EditMachineWindow(Machine machineToEdit)
        {
            InitializeComponent();
            _machineToEdit = machineToEdit;

            AliasTextBox.Text = _machineToEdit.MachineAlias;
            IpAddressTextBox.Text = _machineToEdit.IpAddress;

            LoadExistingLocations();
            LocationComboBox.Text = _machineToEdit.Location ?? "";
        }

        private void LoadExistingLocations()
        {
            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();
                    var sql = "SELECT DISTINCT location FROM machines WHERE location IS NOT NULL AND location != '' ORDER BY location";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            LocationComboBox.Items.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors loading locations
            }
        }

        // *** ADDED: Allow dragging window ***
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // *** ADDED: Close button handler ***
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AliasTextBox.Text) || string.IsNullOrWhiteSpace(IpAddressTextBox.Text))
            {
                MessageBox.Show("الرجاء ملء جميع الحقول", "حقول فارغة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string alias = AliasTextBox.Text.Trim();
                string ipAddress = IpAddressTextBox.Text.Trim();

                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();

                    // Check if IP already exists (exclude current machine)
                    var checkSql = "SELECT COUNT(*) FROM machines WHERE ip_address = @ip AND id != @id";
                    using (var checkCmd = new NpgsqlCommand(checkSql, connection))
                    {
                        checkCmd.Parameters.AddWithValue("ip", ipAddress);
                        checkCmd.Parameters.AddWithValue("id", _machineToEdit.Id);
                        var count = (long)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("جهاز آخر بنفس عنوان IP موجود بالفعل!\n\nAnother device with this IP address already exists!",
                                "Duplicate IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                            IpAddressTextBox.Focus();
                            return;
                        }
                    }

                    // Check if alias already exists (exclude current machine)
                    var checkAliasSql = "SELECT COUNT(*) FROM machines WHERE machine_alias = @alias AND id != @id";
                    using (var checkAliasCmd = new NpgsqlCommand(checkAliasSql, connection))
                    {
                        checkAliasCmd.Parameters.AddWithValue("alias", alias);
                        checkAliasCmd.Parameters.AddWithValue("id", _machineToEdit.Id);
                        var count = (long)checkAliasCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("جهاز آخر بنفس الاسم موجود بالفعل!\n\nAnother device with this name already exists!",
                                "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                            AliasTextBox.Focus();
                            return;
                        }
                    }

                    string location = LocationComboBox.Text?.Trim();

                    var sql = "UPDATE machines SET machine_alias = @alias, ip_address = @ip, location = @location WHERE id = @id";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("alias", alias);
                        cmd.Parameters.AddWithValue("ip", ipAddress);
                        cmd.Parameters.AddWithValue("location", string.IsNullOrEmpty(location) ? (object)DBNull.Value : location);
                        cmd.Parameters.AddWithValue("id", _machineToEdit.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Log the update
                string oldLocation = !string.IsNullOrEmpty(_machineToEdit.Location) ? $", الموقع: {_machineToEdit.Location}" : "";
                string newLocation = !string.IsNullOrWhiteSpace(LocationComboBox.Text?.Trim()) ? $", الموقع: {LocationComboBox.Text.Trim()}" : "";
                var oldValue = $"الاسم: {_machineToEdit.MachineAlias}, IP: {_machineToEdit.IpAddress}{oldLocation}";
                var newValue = $"الاسم: {AliasTextBox.Text.Trim()}, IP: {IpAddressTextBox.Text.Trim()}{newLocation}";
                AuditLogger.Log("UPDATE", "machines", _machineToEdit.Id, oldValue, newValue,
                    $"تعديل بيانات الجهاز: {_machineToEdit.MachineAlias}");

                MessageBox.Show("تم تحديث الجهاز بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحديث الجهاز:\n\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
        }
    }
}
