using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using zkemkeeper;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class DeviceSettingsWindow : Window
    {
        private Machine _machine;
        private CZKEM _zk;
        private bool _isConnected = false;

        public DeviceSettingsWindow(Machine machine)
        {
            InitializeComponent();
            _machine = machine;
            _zk = new CZKEM();

            TitleText.Text = $"إعدادات الجهاز - {machine.MachineAlias}";
            DeviceInfoText.Text = $"الجهاز: {machine.MachineAlias} | IP: {machine.IpAddress}";

            // Initialize manual time controls with current time
            ManualDatePicker.SelectedDate = DateTime.Now.Date;
            ManualHourBox.Text = DateTime.Now.Hour.ToString("00");
            ManualMinuteBox.Text = DateTime.Now.Minute.ToString("00");
            ManualSecondBox.Text = DateTime.Now.Second.ToString("00");

            this.Loaded += DeviceSettingsWindow_Loaded;
            this.Closed += DeviceSettingsWindow_Closed;
        }

        private async void DeviceSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ConnectAndLoadSettings();
        }

        private void DeviceSettingsWindow_Closed(object sender, EventArgs e)
        {
            if (_isConnected)
            {
                try
                {
                    _zk.EnableDevice(1, true);
                    _zk.Disconnect();
                }
                catch { }
            }
        }

        private async Task ConnectAndLoadSettings()
        {
            this.Cursor = Cursors.Wait;

            try
            {
                _isConnected = await Task.Run(() => _zk.Connect_Net(_machine.IpAddress, 4370));

                if (!_isConnected)
                {
                    MessageBox.Show("لا يمكن الاتصال بالجهاز. تأكد من أن الجهاز متصل بالشبكة.",
                        "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _zk.EnableDevice(1, false);

                // Load current settings
                await LoadDeviceSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء الاتصال:\n\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private async Task LoadDeviceSettings()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Read device time
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                    if (_zk.GetDeviceTime(1, ref year, ref month, ref day, ref hour, ref minute, ref second))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DeviceTimeText.Text = $"{year:0000}/{month:00}/{day:00} {hour:00}:{minute:00}:{second:00}";
                        });
                    }

                    // Read device statistics
                    int userCount = 0, fpCount = 0, faceCount = 0, recordCount = 0;
                    _zk.GetDeviceStatus(1, 2, ref userCount);    // User count
                    _zk.GetDeviceStatus(1, 3, ref fpCount);      // Fingerprint count
                    _zk.GetDeviceStatus(1, 21, ref faceCount);   // Face template count
                    _zk.GetDeviceStatus(1, 6, ref recordCount);  // Attendance record count

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UserCountText.Text = userCount.ToString();
                        FingerprintCountText.Text = fpCount.ToString();
                        FaceCountText.Text = faceCount.ToString();
                        RecordCountText.Text = recordCount.ToString();
                    });

                    // Read voice setting
                    string voiceValue = "";
                    if (_zk.GetSysOption(1, "Voice", out voiceValue))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            VoiceToggle.IsChecked = voiceValue == "1";
                        });
                    }

                    // Read volume setting
                    string volumeValue = "";
                    if (_zk.GetSysOption(1, "~Volume", out volumeValue))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (int.TryParse(volumeValue, out int volume))
                            {
                                VolumeSlider.Value = volume;
                            }
                        });
                    }

                    // Read idle time setting
                    string idleValue = "";
                    if (_zk.GetSysOption(1, "IdleMinute", out idleValue))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            IdleTimeTextBox.Text = idleValue;
                        });
                    }

                    // Read lock setting
                    string lockValue = "";
                    if (_zk.GetSysOption(1, "LockPowerKey", out lockValue))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LockDeviceToggle.IsChecked = lockValue == "1";
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                }
            });
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

        #region Time Operations
        private async void ReadDeviceTime_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("الجهاز غير متصل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.Cursor = Cursors.Wait;
            try
            {
                await Task.Run(() =>
                {
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                    if (_zk.GetDeviceTime(1, ref year, ref month, ref day, ref hour, ref minute, ref second))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DeviceTimeText.Text = $"{year:0000}/{month:00}/{day:00} {hour:00}:{minute:00}:{second:00}";
                        });
                    }
                });
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private async void SyncTime_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("الجهاز غير متصل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.Cursor = Cursors.Wait;
            try
            {
                var now = DateTime.Now;
                bool success = await Task.Run(() =>
                    _zk.SetDeviceTime2(1, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second));

                if (success)
                {
                    DeviceTimeText.Text = now.ToString("yyyy/MM/dd HH:mm:ss");
                    MessageBox.Show("تمت مزامنة الوقت بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Log the action
                    AuditLogger.Log("UPDATE", "machines", _machine.Id, null,
                        $"مزامنة وقت الجهاز: {now:yyyy/MM/dd HH:mm:ss}",
                        $"مزامنة وقت جهاز {_machine.MachineAlias}");
                }
                else
                {
                    MessageBox.Show("فشلت مزامنة الوقت", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private async void ApplyManualTime_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("الجهاز غير متصل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate inputs
            if (!ManualDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("الرجاء تحديد التاريخ", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ManualHourBox.Text, out int hour) || hour < 0 || hour > 23)
            {
                MessageBox.Show("الرجاء إدخال ساعة صحيحة (0-23)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ManualMinuteBox.Text, out int minute) || minute < 0 || minute > 59)
            {
                MessageBox.Show("الرجاء إدخال دقيقة صحيحة (0-59)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ManualSecondBox.Text, out int second) || second < 0 || second > 59)
            {
                MessageBox.Show("الرجاء إدخال ثانية صحيحة (0-59)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedDate = ManualDatePicker.SelectedDate.Value;
            var newTime = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, hour, minute, second);

            this.Cursor = Cursors.Wait;
            try
            {
                bool success = await Task.Run(() =>
                    _zk.SetDeviceTime2(1, newTime.Year, newTime.Month, newTime.Day, newTime.Hour, newTime.Minute, newTime.Second));

                if (success)
                {
                    DeviceTimeText.Text = newTime.ToString("yyyy/MM/dd HH:mm:ss");
                    MessageBox.Show("تم تعديل الوقت بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Log the action
                    AuditLogger.Log("UPDATE", "machines", _machine.Id, null,
                        $"تعديل وقت الجهاز يدوياً: {newTime:yyyy/MM/dd HH:mm:ss}",
                        $"تعديل وقت جهاز {_machine.MachineAlias}");
                }
                else
                {
                    MessageBox.Show("فشل تعديل الوقت", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }
        #endregion

        #region Statistics
        private async void RefreshStats_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("الجهاز غير متصل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.Cursor = Cursors.Wait;
            try
            {
                await Task.Run(() =>
                {
                    int userCount = 0, fpCount = 0, faceCount = 0, recordCount = 0;
                    _zk.GetDeviceStatus(1, 2, ref userCount);    // User count
                    _zk.GetDeviceStatus(1, 3, ref fpCount);      // Fingerprint count
                    _zk.GetDeviceStatus(1, 21, ref faceCount);   // Face template count
                    _zk.GetDeviceStatus(1, 6, ref recordCount);  // Attendance record count

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UserCountText.Text = userCount.ToString();
                        FingerprintCountText.Text = fpCount.ToString();
                        FaceCountText.Text = faceCount.ToString();
                        RecordCountText.Text = recordCount.ToString();
                    });
                });

                MessageBox.Show("تم تحديث الإحصائيات بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }
        #endregion

        #region Save Settings
        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("الجهاز غير متصل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.Cursor = Cursors.Wait;
            try
            {
                bool allSuccess = true;
                var changes = new System.Text.StringBuilder();

                await Task.Run(() =>
                {
                    // Save voice setting
                    string voiceValue = VoiceToggle.IsChecked == true ? "1" : "0";
                    if (!_zk.SetSysOption(1, "Voice", voiceValue))
                        allSuccess = false;
                    else
                        changes.AppendLine($"Voice: {voiceValue}");

                    // Save volume setting
                    int volume = 0;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        volume = (int)VolumeSlider.Value;
                    });
                    if (!_zk.SetSysOption(1, "~Volume", volume.ToString()))
                        allSuccess = false;
                    else
                        changes.AppendLine($"Volume: {volume}");

                    // Save idle time setting
                    string idleTime = "";
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        idleTime = IdleTimeTextBox.Text;
                    });
                    if (!_zk.SetSysOption(1, "IdleMinute", idleTime))
                        allSuccess = false;
                    else
                        changes.AppendLine($"IdleMinute: {idleTime}");

                    // Save lock setting
                    string lockValue = LockDeviceToggle.IsChecked == true ? "1" : "0";
                    if (!_zk.SetSysOption(1, "LockPowerKey", lockValue))
                        allSuccess = false;
                    else
                        changes.AppendLine($"LockPowerKey: {lockValue}");
                });

                if (allSuccess)
                {
                    // Log the action
                    AuditLogger.Log("UPDATE", "machines", _machine.Id, null,
                        changes.ToString().Trim(),
                        $"تعديل إعدادات جهاز {_machine.MachineAlias}");

                    MessageBox.Show("تم حفظ الإعدادات بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("تم حفظ بعض الإعدادات، لكن بعضها فشل.\nقد لا يدعم الجهاز جميع الخيارات.",
                        "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }
        #endregion

        #region Device Operations
        private async void RestartDevice_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("الجهاز غير متصل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("هل أنت متأكد من إعادة تشغيل الجهاز؟",
                "تأكيد إعادة التشغيل", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            this.Cursor = Cursors.Wait;
            try
            {
                bool success = await Task.Run(() => _zk.RestartDevice(1));

                if (success)
                {
                    AuditLogger.Log("UPDATE", "machines", _machine.Id, null, "إعادة تشغيل الجهاز",
                        $"إعادة تشغيل جهاز {_machine.MachineAlias}");

                    MessageBox.Show("تم إرسال أمر إعادة التشغيل بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    _isConnected = false;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("فشل إرسال أمر إعادة التشغيل", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private async void PowerOffDevice_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("الجهاز غير متصل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("هل أنت متأكد من إيقاف تشغيل الجهاز؟\n\nسيتعين عليك تشغيل الجهاز يدوياً مرة أخرى.",
                "تأكيد إيقاف التشغيل", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            this.Cursor = Cursors.Wait;
            try
            {
                bool success = await Task.Run(() => _zk.PowerOffDevice(1));

                if (success)
                {
                    AuditLogger.Log("UPDATE", "machines", _machine.Id, null, "إيقاف تشغيل الجهاز",
                        $"إيقاف تشغيل جهاز {_machine.MachineAlias}");

                    MessageBox.Show("تم إرسال أمر إيقاف التشغيل بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    _isConnected = false;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("فشل إرسال أمر إيقاف التشغيل", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }
        #endregion
    }
}
