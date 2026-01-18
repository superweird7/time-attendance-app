using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Leave;

namespace ZKTecoManager
{
    public partial class EmployeeLeaveBalanceWindow : Window
    {
        private readonly User _employee;
        private int _selectedYear;
        private List<LeaveType> _leaveTypes;

        public EmployeeLeaveBalanceWindow(User employee, int year)
        {
            InitializeComponent();
            _employee = employee;
            _selectedYear = year;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set employee info
            EmployeeBadgeText.Text = _employee.BadgeNumber;
            EmployeeNameText.Text = _employee.Name;

            // Initialize year selector
            var currentYear = DateTime.Now.Year;
            var years = new List<int>();
            for (int y = currentYear - 2; y <= currentYear + 1; y++)
            {
                years.Add(y);
            }
            YearComboBox.ItemsSource = years;
            YearComboBox.SelectedItem = _selectedYear;

            await LoadBalancesAsync();
        }

        private async void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (YearComboBox.SelectedItem != null)
            {
                _selectedYear = (int)YearComboBox.SelectedItem;
                await LoadBalancesAsync();
            }
        }

        private async Task LoadBalancesAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                var leaveRepo = ServiceLocator.LeaveRepository;

                // Load leave types
                _leaveTypes = await leaveRepo.GetAllLeaveTypesAsync(false);

                // Load balances
                var balances = await leaveRepo.GetBalancesByUserAsync(_employee.UserId, _selectedYear);

                // Create display items
                var displayItems = new List<LeaveBalanceDisplay>();

                foreach (var leaveType in _leaveTypes.Where(lt => lt.IsActive).OrderBy(lt => lt.DisplayOrder))
                {
                    var balance = balances.FirstOrDefault(b => b.LeaveTypeId == leaveType.LeaveTypeId);

                    var displayItem = new LeaveBalanceDisplay
                    {
                        LeaveTypeId = leaveType.LeaveTypeId,
                        LeaveTypeCode = leaveType.LeaveTypeCode,
                        LeaveTypeNameAr = leaveType.LeaveTypeNameAr,
                        LeaveTypeNameEn = leaveType.LeaveTypeNameEn,
                        TotalAccrued = balance?.TotalAccrued ?? 0,
                        UsedDays = balance?.UsedDays ?? 0,
                        CarriedOver = balance?.CarriedOver ?? 0,
                        ManualAdjustment = balance?.ManualAdjustment ?? 0,
                        RemainingDays = balance?.RemainingDays ?? 0,
                        AdjustButtonVisibility = CurrentUser.IsSuperAdmin ? Visibility.Visible : Visibility.Collapsed
                    };

                    // Set colors based on leave type
                    switch (leaveType.LeaveTypeCode?.ToUpper())
                    {
                        case "ORDINARY":
                            displayItem.CardBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
                            displayItem.CardForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46"));
                            break;
                        case "SICK_FULL":
                            displayItem.CardBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
                            displayItem.CardForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF"));
                            break;
                        case "SICK_HALF":
                            displayItem.CardBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F3FF"));
                            displayItem.CardForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B21B6"));
                            break;
                        case "UNPAID":
                            displayItem.CardBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2"));
                            displayItem.CardForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                            break;
                        default:
                            displayItem.CardBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
                            displayItem.CardForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
                            break;
                    }

                    displayItems.Add(displayItem);
                }

                BalanceCardsControl.ItemsSource = displayItems;

                // Load hourly accumulator
                var hourlyAcc = await leaveRepo.GetHourlyAccumulatorAsync(_employee.UserId);
                if (hourlyAcc != null)
                {
                    HourlyAccText.Text = hourlyAcc.AccumulatedHours.ToString("N2");
                    TotalConvertedText.Text = hourlyAcc.TotalHoursConverted.ToString("N0");
                    TotalDaysDeductedText.Text = hourlyAcc.TotalDaysDeducted.ToString();
                }
                else
                {
                    HourlyAccText.Text = "0";
                    TotalConvertedText.Text = "0";
                    TotalDaysDeductedText.Text = "0";
                }

                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"خطأ في تحميل الأرصدة:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AdjustBalance_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.IsSuperAdmin)
            {
                MessageBox.Show("فقط المدير الرئيسي يمكنه تعديل الأرصدة", "غير مسموح",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            var balanceDisplay = button?.Tag as LeaveBalanceDisplay;
            if (balanceDisplay == null) return;

            // Show adjustment dialog
            var adjustWindow = new AdjustBalanceDialog(
                _employee,
                balanceDisplay.LeaveTypeId,
                balanceDisplay.LeaveTypeNameAr,
                _selectedYear,
                balanceDisplay.RemainingDays);

            adjustWindow.Owner = this;

            if (adjustWindow.ShowDialog() == true)
            {
                await LoadBalancesAsync();
                this.DialogResult = true; // Signal that changes were made
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
            this.Close();
        }

        #endregion
    }

    /// <summary>
    /// Display model for balance cards
    /// </summary>
    public class LeaveBalanceDisplay
    {
        public int LeaveTypeId { get; set; }
        public string LeaveTypeCode { get; set; }
        public string LeaveTypeNameAr { get; set; }
        public string LeaveTypeNameEn { get; set; }
        public decimal TotalAccrued { get; set; }
        public decimal UsedDays { get; set; }
        public decimal CarriedOver { get; set; }
        public decimal ManualAdjustment { get; set; }
        public decimal RemainingDays { get; set; }
        public Brush CardBackground { get; set; }
        public Brush CardForeground { get; set; }
        public Visibility AdjustButtonVisibility { get; set; }
    }
}
