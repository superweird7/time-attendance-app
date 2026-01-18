using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Leave;

namespace ZKTecoManager
{
    public partial class AddLeaveTransactionWindow : Window
    {
        private readonly User _employee;
        private readonly bool _isHourlyMode;
        private List<LeaveType> _leaveTypes;

        public AddLeaveTransactionWindow(User employee, bool isHourlyMode)
        {
            InitializeComponent();
            _employee = employee;
            _isHourlyMode = isHourlyMode;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set employee info
            EmployeeBadgeText.Text = _employee.BadgeNumber;
            EmployeeNameText.Text = _employee.Name;

            // Set default dates
            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today;
            HourlyDatePicker.SelectedDate = DateTime.Today;

            // Initialize time comboboxes
            InitializeTimeComboBoxes();

            // Configure mode
            if (_isHourlyMode)
            {
                HeaderIcon.Text = "⏰";
                HeaderTitle.Text = "تسجيل إجازة ساعية";
                RegularLeaveSection.Visibility = Visibility.Collapsed;
                HourlyLeaveSection.Visibility = Visibility.Visible;
            }
            else
            {
                RegularLeaveSection.Visibility = Visibility.Visible;
                HourlyLeaveSection.Visibility = Visibility.Collapsed;
                await LoadLeaveTypesAsync();
            }
        }

        private void InitializeTimeComboBoxes()
        {
            // Hours (7-17 for typical work day)
            var hours = Enumerable.Range(7, 11).Select(h => h.ToString("D2")).ToList();
            StartHourComboBox.ItemsSource = hours;
            EndHourComboBox.ItemsSource = hours;
            StartHourComboBox.SelectedIndex = 0;  // 07
            EndHourComboBox.SelectedIndex = 1;    // 08

            // Minutes (00, 15, 30, 45)
            var minutes = new List<string> { "00", "15", "30", "45" };
            StartMinuteComboBox.ItemsSource = minutes;
            EndMinuteComboBox.ItemsSource = minutes;
            StartMinuteComboBox.SelectedIndex = 0;
            EndMinuteComboBox.SelectedIndex = 0;
        }

        private async Task LoadLeaveTypesAsync()
        {
            try
            {
                var leaveRepo = ServiceLocator.LeaveRepository;
                _leaveTypes = await leaveRepo.GetAllLeaveTypesAsync(true);
                LeaveTypeComboBox.ItemsSource = _leaveTypes;
                if (_leaveTypes.Any())
                {
                    LeaveTypeComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل أنواع الإجازات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against event firing during InitializeComponent
            if (CalculatedDaysText == null) return;
            CalculateDays();
        }

        private void CalculateDays()
        {
            if (CalculatedDaysText == null) return;
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                var start = StartDatePicker.SelectedDate.Value;
                var end = EndDatePicker.SelectedDate.Value;

                if (end >= start)
                {
                    var days = (end - start).Days + 1;
                    CalculatedDaysText.Text = days.ToString();
                }
                else
                {
                    CalculatedDaysText.Text = "0";
                }
            }
        }

        private void HourlyMethodChanged(object sender, RoutedEventArgs e)
        {
            // Guard against event firing during InitializeComponent
            if (TimeRangePanel == null || DirectHoursPanel == null) return;

            if (TimeRangeRadio.IsChecked == true)
            {
                TimeRangePanel.Visibility = Visibility.Visible;
                DirectHoursPanel.Visibility = Visibility.Collapsed;
                CalculateTimeRangeHours();
            }
            else
            {
                TimeRangePanel.Visibility = Visibility.Collapsed;
                DirectHoursPanel.Visibility = Visibility.Visible;
                CalculatedHoursText.Text = "0";
            }
        }

        private void TimeRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Guard against event firing during InitializeComponent
            if (CalculatedHoursText == null) return;
            CalculateTimeRangeHours();
        }

        private void CalculateTimeRangeHours()
        {
            if (StartHourComboBox.SelectedItem == null || EndHourComboBox.SelectedItem == null ||
                StartMinuteComboBox.SelectedItem == null || EndMinuteComboBox.SelectedItem == null)
            {
                return;
            }

            try
            {
                var startHour = int.Parse(StartHourComboBox.SelectedItem.ToString());
                var startMinute = int.Parse(StartMinuteComboBox.SelectedItem.ToString());
                var endHour = int.Parse(EndHourComboBox.SelectedItem.ToString());
                var endMinute = int.Parse(EndMinuteComboBox.SelectedItem.ToString());

                var startTime = new TimeSpan(startHour, startMinute, 0);
                var endTime = new TimeSpan(endHour, endMinute, 0);

                if (endTime > startTime)
                {
                    var diff = endTime - startTime;
                    var hours = diff.TotalHours;
                    CalculatedHoursText.Text = hours.ToString("N2");
                }
                else
                {
                    CalculatedHoursText.Text = "0";
                }
            }
            catch
            {
                CalculatedHoursText.Text = "0";
            }
        }

        private void DirectHours_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(DirectHoursTextBox.Text, out var hours) && hours > 0)
            {
                CalculatedHoursText.Text = hours.ToString("N2");
            }
            else
            {
                CalculatedHoursText.Text = "0";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var leaveService = ServiceLocator.LeaveManagementService;

                if (_isHourlyMode)
                {
                    await SaveHourlyLeaveAsync(leaveService);
                }
                else
                {
                    await SaveRegularLeaveAsync(leaveService);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الإجازة:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveRegularLeaveAsync(Services.Interfaces.ILeaveManagementService leaveService)
        {
            // Validate
            var selectedLeaveType = LeaveTypeComboBox.SelectedItem as LeaveType;
            if (selectedLeaveType == null)
            {
                MessageBox.Show("الرجاء اختيار نوع الإجازة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("الرجاء تحديد تاريخ البداية والنهاية", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var startDate = StartDatePicker.SelectedDate.Value;
            var endDate = EndDatePicker.SelectedDate.Value;

            if (endDate < startDate)
            {
                MessageBox.Show("تاريخ النهاية يجب أن يكون بعد تاريخ البداية", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var reason = ReasonTextBox.Text.Trim();

            // Call service
            var result = await leaveService.DeductLeaveAsync(
                _employee.UserId,
                selectedLeaveType.LeaveTypeId,
                startDate,
                endDate,
                reason,
                CurrentUser.UserId);

            if (result.Success)
            {
                MessageBox.Show(result.Message, "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show(result.Message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task SaveHourlyLeaveAsync(Services.Interfaces.ILeaveManagementService leaveService)
        {
            if (!HourlyDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("الرجاء تحديد تاريخ الإجازة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var submissionDate = HourlyDatePicker.SelectedDate.Value;
            var reason = ReasonTextBox.Text.Trim();

            (bool Success, string Message) result;

            if (TimeRangeRadio.IsChecked == true)
            {
                // Time range method
                var startHour = int.Parse(StartHourComboBox.SelectedItem.ToString());
                var startMinute = int.Parse(StartMinuteComboBox.SelectedItem.ToString());
                var endHour = int.Parse(EndHourComboBox.SelectedItem.ToString());
                var endMinute = int.Parse(EndMinuteComboBox.SelectedItem.ToString());

                var startTime = new TimeSpan(startHour, startMinute, 0);
                var endTime = new TimeSpan(endHour, endMinute, 0);

                if (endTime <= startTime)
                {
                    MessageBox.Show("وقت النهاية يجب أن يكون بعد وقت البداية", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                result = await leaveService.AddHourlyLeaveByTimeRangeAsync(
                    _employee.UserId,
                    startTime,
                    endTime,
                    submissionDate,
                    reason,
                    CurrentUser.UserId);
            }
            else
            {
                // Direct hours method
                if (!decimal.TryParse(DirectHoursTextBox.Text, out var hours) || hours <= 0)
                {
                    MessageBox.Show("الرجاء إدخال عدد ساعات صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                result = await leaveService.AddHourlyLeaveAsync(
                    _employee.UserId,
                    hours,
                    submissionDate,
                    reason,
                    CurrentUser.UserId);
            }

            if (result.Success)
            {
                MessageBox.Show(result.Message, "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show(result.Message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #region Window Controls

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        #endregion
    }
}
