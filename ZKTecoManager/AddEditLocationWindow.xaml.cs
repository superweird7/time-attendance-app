using System;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Data.Repositories;
using ZKTecoManager.Models.Sync;
using ZKTecoManager.Services;

namespace ZKTecoManager
{
    /// <summary>
    /// Interaction logic for AddEditLocationWindow.xaml
    /// </summary>
    public partial class AddEditLocationWindow : Window
    {
        private readonly RemoteLocationRepository _repository;
        private readonly RemoteSyncService _syncService;
        private readonly RemoteLocation _existingLocation;
        private readonly bool _isEditMode;

        public AddEditLocationWindow()
        {
            InitializeComponent();
            _repository = new RemoteLocationRepository();
            _syncService = new RemoteSyncService();
            _isEditMode = false;
            TitleText.Text = "إضافة موقع جديد";
        }

        public AddEditLocationWindow(RemoteLocation location) : this()
        {
            _existingLocation = location;
            _isEditMode = true;
            TitleText.Text = "تعديل الموقع";
            LoadLocation();
        }

        private void LoadLocation()
        {
            if (_existingLocation == null) return;

            LocationNameTextBox.Text = _existingLocation.LocationName;
            HostTextBox.Text = _existingLocation.Host;
            PortTextBox.Text = _existingLocation.Port.ToString();
            DatabaseTextBox.Text = _existingLocation.DatabaseName;
            UsernameTextBox.Text = _existingLocation.Username;
            PasswordBox.Password = _existingLocation.Password;
            IsActiveCheckBox.IsChecked = _existingLocation.IsActive;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
            {
                return;
            }

            var location = GetLocationFromForm();

            try
            {
                var canConnect = await _syncService.TestConnectionAsync(location);
                if (canConnect)
                {
                    MessageBox.Show("تم الاتصال بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("فشل الاتصال. تأكد من صحة البيانات.", "فشل", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الاتصال: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
            {
                return;
            }

            var location = GetLocationFromForm();

            try
            {
                if (_isEditMode)
                {
                    location.LocationId = _existingLocation.LocationId;
                    await _repository.UpdateAsync(location);
                    MessageBox.Show("تم تحديث الموقع بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _repository.AddAsync(location);
                    MessageBox.Show("تم إضافة الموقع بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الموقع: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(LocationNameTextBox.Text))
            {
                MessageBox.Show("الرجاء إدخال اسم الموقع", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                LocationNameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(HostTextBox.Text))
            {
                MessageBox.Show("الرجاء إدخال عنوان الخادم", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                HostTextBox.Focus();
                return false;
            }

            if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("الرجاء إدخال رقم منفذ صحيح (1-65535)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                PortTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(DatabaseTextBox.Text))
            {
                MessageBox.Show("الرجاء إدخال اسم قاعدة البيانات", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                DatabaseTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("الرجاء إدخال اسم المستخدم", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("الرجاء إدخال كلمة المرور", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return false;
            }

            return true;
        }

        private RemoteLocation GetLocationFromForm()
        {
            return new RemoteLocation
            {
                LocationName = LocationNameTextBox.Text.Trim(),
                Host = HostTextBox.Text.Trim(),
                Port = int.Parse(PortTextBox.Text),
                DatabaseName = DatabaseTextBox.Text.Trim(),
                Username = UsernameTextBox.Text.Trim(),
                Password = PasswordBox.Password,
                IsActive = IsActiveCheckBox.IsChecked == true
            };
        }
    }
}
