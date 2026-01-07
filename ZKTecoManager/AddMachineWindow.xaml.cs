using Npgsql;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class AddMachineWindow : Window
    {

        public AddMachineWindow()
        {
            InitializeComponent();
            LoadExistingLocations();
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
            string alias = AliasTextBox.Text.Trim();
            string ipAddress = IpAddressTextBox.Text.Trim();

            // Validate inputs
            if (string.IsNullOrWhiteSpace(alias))
            {
                MessageBox.Show("Please enter a device alias.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                AliasTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                MessageBox.Show("Please enter an IP address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                IpAddressTextBox.Focus();
                return;
            }

            // Validate IP address format
            if (!IsValidIPAddress(ipAddress))
            {
                MessageBox.Show("Please enter a valid IP address (e.g., 192.168.1.100).", "Invalid IP Address", MessageBoxButton.OK, MessageBoxImage.Warning);
                IpAddressTextBox.Focus();
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();

                    // Check if IP already exists
                    var checkSql = "SELECT COUNT(*) FROM machines WHERE ip_address = @ip";
                    using (var checkCmd = new NpgsqlCommand(checkSql, connection))
                    {
                        checkCmd.Parameters.AddWithValue("ip", ipAddress);
                        var count = (long)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("جهاز بنفس عنوان IP موجود بالفعل!\n\nA device with this IP address already exists!",
                                "Duplicate IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                            IpAddressTextBox.Focus();
                            return;
                        }
                    }

                    // Check if alias already exists
                    var checkAliasSql = "SELECT COUNT(*) FROM machines WHERE machine_alias = @alias";
                    using (var checkAliasCmd = new NpgsqlCommand(checkAliasSql, connection))
                    {
                        checkAliasCmd.Parameters.AddWithValue("alias", alias);
                        var count = (long)checkAliasCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("جهاز بنفس الاسم موجود بالفعل!\n\nA device with this name already exists!",
                                "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                            AliasTextBox.Focus();
                            return;
                        }
                    }

                    string location = LocationComboBox.Text?.Trim();

                    var sql = "INSERT INTO machines (machine_alias, ip_address, location) VALUES (@alias, @ip, @location)";
                    using (var cmd = new NpgsqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("alias", alias);
                        cmd.Parameters.AddWithValue("ip", ipAddress);
                        cmd.Parameters.AddWithValue("location", string.IsNullOrEmpty(location) ? (object)DBNull.Value : location);
                        cmd.ExecuteNonQuery();
                    }
                }

                string locationText = !string.IsNullOrWhiteSpace(LocationComboBox.Text?.Trim())
                    ? $", الموقع: {LocationComboBox.Text.Trim()}"
                    : "";

                // Log the addition
                AuditLogger.Log("INSERT", "machines", null, null,
                    $"الاسم: {alias}, IP: {ipAddress}{locationText}",
                    $"اضافة جهاز جديد: {alias} ({ipAddress})");

                MessageBox.Show("تم إضافة الجهاز بنجاح!\n\nDevice added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل حفظ الجهاز:\n\nFailed to save device:\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.DialogResult = false;
            }
        }

        // Helper method to validate IP address
        private bool IsValidIPAddress(string ipAddress)
        {
            string pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
            return Regex.IsMatch(ipAddress, pattern);
        }
    }
}