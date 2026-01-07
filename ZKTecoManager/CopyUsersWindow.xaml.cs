using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using zkemkeeper;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class CopyUsersWindow : Window
    {
        private Machine _sourceMachine;
        private List<Machine> _allMachines;
        private Dictionary<int, CheckBox> _targetCheckboxes = new Dictionary<int, CheckBox>();
        private Dictionary<string, CheckBox> _userCheckboxes = new Dictionary<string, CheckBox>();
        private List<DeviceUser> _loadedUsers = new List<DeviceUser>();

        public CopyUsersWindow(Machine sourceMachine, List<Machine> allMachines)
        {
            InitializeComponent();
            _sourceMachine = sourceMachine;
            _allMachines = allMachines.Where(m => m.Id != sourceMachine.Id).ToList();

            SourceDeviceText.Text = $"{sourceMachine.MachineAlias} ({sourceMachine.IpAddress})";

            LoadTargetDevices();
        }

        private void LoadTargetDevices()
        {
            TargetDevicesPanel.Children.Clear();
            _targetCheckboxes.Clear();

            foreach (var machine in _allMachines)
            {
                var checkbox = new CheckBox
                {
                    Content = $"{machine.MachineAlias} ({machine.IpAddress})",
                    Tag = machine,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Tahoma, Arial"),
                    FontSize = 14,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#374151")),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                TargetDevicesPanel.Children.Add(checkbox);
                _targetCheckboxes[machine.Id] = checkbox;
            }

            if (_allMachines.Count == 0)
            {
                var noDevicesText = new TextBlock
                {
                    Text = "لا توجد أجهزة أخرى متاحة",
                    FontSize = 14,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9CA3AF")),
                    FontStyle = FontStyles.Italic
                };
                TargetDevicesPanel.Children.Add(noDevicesText);
                CopyButton.IsEnabled = false;
            }
        }

        #region Window Behavior
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
        #endregion

        #region Target Device Selection
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkbox in _targetCheckboxes.Values)
            {
                checkbox.IsChecked = true;
            }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkbox in _targetCheckboxes.Values)
            {
                checkbox.IsChecked = false;
            }
        }
        #endregion

        #region User Selection
        private void SelectAllUsers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkbox in _userCheckboxes.Values)
            {
                checkbox.IsChecked = true;
            }
        }

        private void DeselectAllUsers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var checkbox in _userCheckboxes.Values)
            {
                checkbox.IsChecked = false;
            }
        }

        private async void LoadUsersFromDevice_Click(object sender, RoutedEventArgs e)
        {
            LoadUsersButton.IsEnabled = false;
            LoadingOverlay.Show("جاري تحميل المستخدمين من الجهاز...", _sourceMachine.MachineAlias);

            try
            {
                _loadedUsers.Clear();
                _userCheckboxes.Clear();
                SourceUsersPanel.Children.Clear();

                await Task.Run(() =>
                {
                    var zk = new CZKEM();

                    if (!zk.Connect_Net(_sourceMachine.IpAddress, 4370))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("لا يمكن الاتصال بالجهاز المصدر",
                                "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return;
                    }

                    try
                    {
                        zk.EnableDevice(1, false);

                        // Only read user info - fingerprints will be read during copy
                        if (zk.ReadAllUserID(1))
                        {
                            string badgeNumber = "", name = "", password = "";
                            int privilege = 0;
                            bool enabled = false;

                            while (zk.SSR_GetAllUserInfo(1, out badgeNumber, out name, out password, out privilege, out enabled))
                            {
                                var user = new DeviceUser
                                {
                                    BadgeNumber = badgeNumber,
                                    Name = string.IsNullOrEmpty(name) ? badgeNumber : name,
                                    Password = password,
                                    Privilege = privilege,
                                    Enabled = enabled,
                                    Fingerprints = new List<DeviceFingerprint>()
                                };

                                _loadedUsers.Add(user);
                            }
                        }
                    }
                    finally
                    {
                        zk.EnableDevice(1, true);
                        zk.Disconnect();
                    }
                });

                // Update UI with loaded users
                if (_loadedUsers.Count > 0)
                {
                    NoUsersText.Visibility = Visibility.Collapsed;
                    UsersScrollViewer.Visibility = Visibility.Visible;
                    UserCountText.Text = $" ({_loadedUsers.Count} مستخدم)";

                    foreach (var user in _loadedUsers.OrderBy(u => u.Name))
                    {
                        string displayText = string.IsNullOrEmpty(user.Name) || user.Name == user.BadgeNumber
                            ? $"{user.BadgeNumber}"
                            : $"{user.Name} ({user.BadgeNumber})";

                        var checkbox = new CheckBox
                        {
                            Content = displayText,
                            Tag = user,
                            IsChecked = true, // Select all by default
                            FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Tahoma, Arial"),
                            FontSize = 13,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#374151")),
                            Cursor = Cursors.Hand,
                            Margin = new Thickness(0, 3, 0, 3)
                        };

                        SourceUsersPanel.Children.Add(checkbox);
                        _userCheckboxes[user.BadgeNumber] = checkbox;
                    }

                    MessageBox.Show($"تم تحميل {_loadedUsers.Count} مستخدم بنجاح!",
                        "اكتمل التحميل", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    NoUsersText.Text = "لا يوجد مستخدمين في الجهاز المصدر";
                    NoUsersText.Visibility = Visibility.Visible;
                    UsersScrollViewer.Visibility = Visibility.Collapsed;
                    UserCountText.Text = " (0 مستخدم)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تحميل المستخدمين:\n\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadUsersButton.IsEnabled = true;
                LoadingOverlay.Hide();
            }
        }
        #endregion

        #region Copy Operation
        private async void StartCopy_Click(object sender, RoutedEventArgs e)
        {
            // Get selected users
            var selectedUsers = _userCheckboxes
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => _loadedUsers.FirstOrDefault(u => u.BadgeNumber == kv.Key))
                .Where(u => u != null)
                .ToList();

            if (selectedUsers.Count == 0)
            {
                // If no users loaded yet, load them first
                if (_loadedUsers.Count == 0)
                {
                    MessageBox.Show("الرجاء تحميل المستخدمين من الجهاز المصدر أولاً",
                        "لا يوجد مستخدمين", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show("الرجاء تحديد مستخدم واحد على الأقل للنسخ",
                    "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get selected target devices
            var selectedTargets = _targetCheckboxes
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => kv.Value.Tag as Machine)
                .Where(m => m != null)
                .ToList();

            if (selectedTargets.Count == 0)
            {
                MessageBox.Show("الرجاء تحديد جهاز واحد على الأقل كهدف للنسخ",
                    "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool copyFingerprints = CopyFingerprintsCheckBox.IsChecked == true;
            bool copyPasswords = CopyPasswordsCheckBox.IsChecked == true;
            bool overwriteExisting = OverwriteExistingCheckBox.IsChecked == true;

            CopyButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            LoadingOverlay.Show("جاري النسخ...", "يرجى الانتظار وعدم إغلاق النافذة");

            int totalUsersCopied = 0;
            int totalFingerprintsCopied = 0;
            var failedDevices = new List<string>();

            try
            {
                // Step 1: Read fingerprints from source device if needed
                if (copyFingerprints)
                {
                    LoadingOverlay.Show("جاري قراءة البصمات من الجهاز المصدر...", _sourceMachine.MachineAlias);
                    ProgressText.Text = "جاري قراءة البصمات من الجهاز المصدر...";
                    ProgressDetailText.Text = _sourceMachine.MachineAlias;
                    CopyProgressBar.Value = 0;
                    await Task.Delay(50);

                    await Task.Run(() =>
                    {
                        var sourceZk = new CZKEM();
                        if (sourceZk.Connect_Net(_sourceMachine.IpAddress, 4370))
                        {
                            try
                            {
                                sourceZk.EnableDevice(1, false);

                                foreach (var user in selectedUsers)
                                {
                                    user.Fingerprints.Clear();
                                    for (int fingerIndex = 0; fingerIndex <= 9; fingerIndex++)
                                    {
                                        string fingerTemplate = "";
                                        int templateLength = 0;
                                        int flag = 0;

                                        if (sourceZk.GetUserTmpExStr(1, user.BadgeNumber, fingerIndex, out flag, out fingerTemplate, out templateLength))
                                        {
                                            if (!string.IsNullOrEmpty(fingerTemplate) && templateLength > 0)
                                            {
                                                user.Fingerprints.Add(new DeviceFingerprint
                                                {
                                                    FingerIndex = fingerIndex,
                                                    Template = fingerTemplate,
                                                    Flag = flag
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                sourceZk.EnableDevice(1, true);
                                sourceZk.Disconnect();
                            }
                        }
                    });
                }

                // Step 2: Copy to each target device
                int deviceIndex = 0;
                foreach (var targetMachine in selectedTargets)
                {
                    deviceIndex++;
                    LoadingOverlay.Show($"جاري النسخ إلى الجهاز {deviceIndex} من {selectedTargets.Count}...", targetMachine.MachineAlias);
                    ProgressText.Text = $"جاري النسخ إلى الجهاز {deviceIndex} من {selectedTargets.Count}...";
                    ProgressDetailText.Text = targetMachine.MachineAlias;
                    CopyProgressBar.Value = (deviceIndex * 100.0) / selectedTargets.Count;
                    await Task.Delay(50);

                    var targetZk = new CZKEM();
                    bool targetConnected = await Task.Run(() => targetZk.Connect_Net(targetMachine.IpAddress, 4370));

                    if (!targetConnected)
                    {
                        failedDevices.Add(targetMachine.MachineAlias);
                        continue;
                    }

                    try
                    {
                        targetZk.EnableDevice(1, false);

                        // Get existing users if not overwriting
                        var existingBadges = new HashSet<string>();
                        if (!overwriteExisting)
                        {
                            await Task.Run(() =>
                            {
                                if (targetZk.ReadAllUserID(1))
                                {
                                    string badge = "", n = "", p = "";
                                    int priv = 0;
                                    bool en = false;
                                    while (targetZk.SSR_GetAllUserInfo(1, out badge, out n, out p, out priv, out en))
                                    {
                                        existingBadges.Add(badge);
                                    }
                                }
                            });
                        }

                        // Copy each selected user
                        await Task.Run(() =>
                        {
                            foreach (var user in selectedUsers)
                            {
                                // Skip if user exists and we're not overwriting
                                if (!overwriteExisting && existingBadges.Contains(user.BadgeNumber))
                                    continue;

                                // Set user info
                                string passwordToSet = copyPasswords ? user.Password : "";
                                if (targetZk.SSR_SetUserInfo(1, user.BadgeNumber, user.Name, passwordToSet, user.Privilege, user.Enabled))
                                {
                                    totalUsersCopied++;

                                    // Copy fingerprints if enabled
                                    if (copyFingerprints)
                                    {
                                        foreach (var fp in user.Fingerprints)
                                        {
                                            if (targetZk.SetUserTmpExStr(1, user.BadgeNumber, fp.FingerIndex, fp.Flag, fp.Template))
                                            {
                                                totalFingerprintsCopied++;
                                            }
                                        }
                                    }
                                }
                            }
                        });

                        targetZk.RefreshData(1);
                    }
                    finally
                    {
                        targetZk.EnableDevice(1, true);
                        targetZk.Disconnect();
                    }
                }

                // Log the action
                AuditLogger.Log("SYNC", "machines", _sourceMachine.Id, null,
                    $"نسخ {totalUsersCopied} مستخدم و{totalFingerprintsCopied} بصمة إلى {selectedTargets.Count - failedDevices.Count} جهاز",
                    $"نسخ المستخدمين من {_sourceMachine.MachineAlias}");

                // Show results
                string resultMessage = $"اكتملت عملية النسخ!\n\n" +
                    $"المستخدمين المحددين: {selectedUsers.Count}\n" +
                    $"المستخدمين المنسوخين: {totalUsersCopied}\n" +
                    $"البصمات المنسوخة: {totalFingerprintsCopied}\n" +
                    $"الأجهزة الناجحة: {selectedTargets.Count - failedDevices.Count}";

                if (failedDevices.Count > 0)
                {
                    resultMessage += $"\n\nالأجهزة الفاشلة:\n" + string.Join("\n", failedDevices.Select(d => $"• {d}"));
                }

                MessageBox.Show(resultMessage, "اكتمل النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء النسخ:\n\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CopyButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                LoadingOverlay.Hide();
            }
        }
        #endregion

        // Helper classes
        private class DeviceUser
        {
            public string BadgeNumber { get; set; }
            public string Name { get; set; }
            public string Password { get; set; }
            public int Privilege { get; set; }
            public bool Enabled { get; set; }
            public List<DeviceFingerprint> Fingerprints { get; set; }
        }

        private class DeviceFingerprint
        {
            public int FingerIndex { get; set; }
            public string Template { get; set; }
            public int Flag { get; set; }
        }
    }
}
