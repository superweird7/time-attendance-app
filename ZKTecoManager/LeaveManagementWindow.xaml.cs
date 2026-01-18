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
    public partial class LeaveManagementWindow : Window
    {
        private List<Department> _departments;
        private List<User> _employees;
        private User _selectedEmployee;
        private int _selectedYear;
        private int _selectedDepartmentId;

        public LeaveManagementWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                // Initialize year selector
                InitializeYearSelector();

                // Load departments first (employees loaded when department selected)
                await LoadDepartmentsAsync();

                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"خطأ في تحميل البيانات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeYearSelector()
        {
            var currentYear = DateTime.Now.Year;
            var years = new List<int>();
            for (int y = currentYear - 2; y <= currentYear + 1; y++)
            {
                years.Add(y);
            }
            YearComboBox.ItemsSource = years;
            YearComboBox.SelectedItem = currentYear;
            _selectedYear = currentYear;
        }

        private async Task LoadDepartmentsAsync()
        {
            var deptRepo = ServiceLocator.DepartmentRepository;
            var allDepts = await deptRepo.GetAllAsync();

            if (CurrentUser.IsSuperAdmin)
            {
                // Superadmin sees all departments
                _departments = allDepts.OrderBy(d => d.DeptName).ToList();
            }
            else if (CurrentUser.IsLeaveAdmin || CurrentUser.IsDeptAdmin)
            {
                // Department-based admins see only their permitted departments
                if (CurrentUser.PermittedDepartmentIds.Any())
                {
                    _departments = allDepts
                        .Where(d => CurrentUser.PermittedDepartmentIds.Contains(d.DeptId))
                        .OrderBy(d => d.DeptName)
                        .ToList();
                }
                else
                {
                    _departments = new List<Department>();
                }
            }
            else
            {
                _departments = new List<Department>();
            }

            DepartmentComboBox.ItemsSource = _departments;

            // Auto-select first department if available
            if (_departments.Any())
            {
                DepartmentComboBox.SelectedIndex = 0;
            }
        }

        private async void DepartmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dept = DepartmentComboBox.SelectedItem as Department;
            if (dept == null)
            {
                _selectedDepartmentId = 0;
                EmployeeComboBox.IsEnabled = false;
                EmployeeComboBox.ItemsSource = null;
                ClearBalanceCards();
                TransactionsDataGrid.ItemsSource = null;
                return;
            }

            _selectedDepartmentId = dept.DeptId;
            EmployeeComboBox.IsEnabled = true;
            await LoadEmployeesByDepartmentAsync(dept.DeptId);
        }

        private async Task LoadEmployeesByDepartmentAsync(int deptId)
        {
            var userRepo = ServiceLocator.UserRepository;

            // Load employees for the selected department
            var allUsers = await userRepo.GetAllAsync();
            _employees = allUsers
                .Where(u => u.DefaultDeptId == deptId)
                .OrderBy(e => e.BadgeNumber)
                .ToList();

            EmployeeComboBox.ItemsSource = _employees;

            // Select first employee if available
            if (_employees.Any())
            {
                EmployeeComboBox.SelectedIndex = 0;
            }
            else
            {
                _selectedEmployee = null;
                ClearBalanceCards();
                TransactionsDataGrid.ItemsSource = null;
            }
        }

        private async void EmployeeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedEmployee = EmployeeComboBox.SelectedItem as User;
            if (_selectedEmployee != null)
            {
                await RefreshEmployeeDataAsync();
            }
            else
            {
                ClearBalanceCards();
                TransactionsDataGrid.ItemsSource = null;
            }
        }

        private async void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (YearComboBox.SelectedItem != null)
            {
                _selectedYear = (int)YearComboBox.SelectedItem;
                if (_selectedEmployee != null)
                {
                    await RefreshEmployeeDataAsync();
                }
            }
        }

        private async Task RefreshEmployeeDataAsync()
        {
            if (_selectedEmployee == null) return;

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                var leaveRepo = ServiceLocator.LeaveRepository;

                // Load balances
                var balances = await leaveRepo.GetBalancesByUserAsync(_selectedEmployee.UserId, _selectedYear);
                UpdateBalanceCards(balances);

                // Load hourly accumulator
                var hourlyAcc = await leaveRepo.GetHourlyAccumulatorAsync(_selectedEmployee.UserId);
                HourlyAccumulatorText.Text = hourlyAcc?.AccumulatedHours.ToString("N2") ?? "0";

                // Load unpaid used this month
                var unpaidType = await leaveRepo.GetLeaveTypeByCodeAsync("UNPAID");
                if (unpaidType != null)
                {
                    var unpaidUsed = await leaveRepo.GetUsedDaysInMonthAsync(
                        _selectedEmployee.UserId, unpaidType.LeaveTypeId,
                        DateTime.Now.Year, DateTime.Now.Month);
                    UnpaidUsedText.Text = $"{unpaidUsed:N0}/5";
                }

                // Load transactions
                var transactions = await leaveRepo.GetTransactionsByUserAsync(
                    _selectedEmployee.UserId,
                    new DateTime(_selectedYear, 1, 1),
                    new DateTime(_selectedYear, 12, 31));

                // Enrich with display data
                var leaveTypes = await leaveRepo.GetAllLeaveTypesAsync(false);
                var transactionDisplayList = transactions.Select(t => new LeaveTransactionDisplay
                {
                    TransactionId = t.TransactionId,
                    UserBadgeNumber = _selectedEmployee.BadgeNumber,
                    UserName = _selectedEmployee.Name,
                    LeaveTypeName = leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == t.LeaveTypeId)?.LeaveTypeNameAr ?? "غير معروف",
                    TransactionType = t.TransactionType,
                    TransactionTypeDisplay = GetTransactionTypeDisplay(t.TransactionType),
                    DaysAmount = t.DaysAmount,
                    HoursAmount = t.HoursAmount,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    SubmissionDate = t.SubmissionDate,
                    Reason = t.Reason
                }).OrderByDescending(t => t.SubmissionDate).ToList();

                TransactionsDataGrid.ItemsSource = transactionDisplayList;

                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"خطأ في تحميل بيانات الموظف:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateBalanceCards(List<LeaveBalance> balances)
        {
            // Reset all
            OrdinaryBalanceText.Text = "--";
            SickFullBalanceText.Text = "--";
            SickHalfBalanceText.Text = "--";

            foreach (var balance in balances)
            {
                var remaining = balance.RemainingDays;
                switch (balance.LeaveTypeCode?.ToUpper())
                {
                    case "ORDINARY":
                        OrdinaryBalanceText.Text = remaining.ToString("N2");
                        break;
                    case "SICK_FULL":
                        SickFullBalanceText.Text = remaining.ToString("N2");
                        break;
                    case "SICK_HALF":
                        SickHalfBalanceText.Text = remaining.ToString("N2");
                        break;
                }
            }
        }

        private void ClearBalanceCards()
        {
            OrdinaryBalanceText.Text = "--";
            SickFullBalanceText.Text = "--";
            SickHalfBalanceText.Text = "--";
            UnpaidUsedText.Text = "--";
            HourlyAccumulatorText.Text = "--";
        }

        private string GetTransactionTypeDisplay(string transactionType)
        {
            switch (transactionType?.ToLower())
            {
                case "deduction": return "خصم";
                case "accrual": return "استحقاق";
                case "adjustment": return "تعديل";
                case "carryover": return "ترحيل";
                case "hourly_conversion": return "تحويل ساعي";
                case "reset": return "إعادة تعيين";
                default: return transactionType ?? "";
            }
        }

        #region Button Handlers

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshEmployeeDataAsync();
        }

        private void AddLeaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEmployee == null)
            {
                MessageBox.Show("الرجاء اختيار موظف أولاً", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var addWindow = new AddLeaveTransactionWindow(_selectedEmployee, false) { Owner = this };
                if (addWindow.ShowDialog() == true)
                {
                    _ = RefreshEmployeeDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة تسجيل الإجازة:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddHourlyLeaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEmployee == null)
            {
                MessageBox.Show("الرجاء اختيار موظف أولاً", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var addWindow = new AddLeaveTransactionWindow(_selectedEmployee, true) { Owner = this };
                if (addWindow.ShowDialog() == true)
                {
                    _ = RefreshEmployeeDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة الإجازة الساعية:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewBalanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEmployee == null)
            {
                MessageBox.Show("الرجاء اختيار موظف أولاً", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var balanceWindow = new EmployeeLeaveBalanceWindow(_selectedEmployee, _selectedYear) { Owner = this };
            if (balanceWindow.ShowDialog() == true)
            {
                _ = RefreshEmployeeDataAsync();
            }
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportsWindow = new LeaveReportsWindow { Owner = this };
                reportsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح نافذة التقارير:\n{ex.Message}\n\n{ex.StackTrace}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

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
    /// Display model for transactions in the DataGrid
    /// </summary>
    public class LeaveTransactionDisplay
    {
        public int TransactionId { get; set; }
        public string UserBadgeNumber { get; set; }
        public string UserName { get; set; }
        public string LeaveTypeName { get; set; }
        public string TransactionType { get; set; }
        public string TransactionTypeDisplay { get; set; }
        public decimal DaysAmount { get; set; }
        public decimal? HoursAmount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime SubmissionDate { get; set; }
        public string Reason { get; set; }
    }
}
