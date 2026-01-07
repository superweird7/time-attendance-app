using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Dashboard;

namespace ZKTecoManager
{
    /// <summary>
    /// Dashboard window showing real-time attendance KPIs.
    /// </summary>
    public partial class DashboardWindow : Window
    {
        private DispatcherTimer _autoRefreshTimer;
        private List<Department> _departments;
        private bool _isLoading;

        public DashboardWindow()
        {
            InitializeComponent();
            this.KeyDown += DashboardWindow_KeyDown;
        }

        private async void DashboardWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseButton_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                await LoadDashboardDataAsync();
                e.Handled = true;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set default date to today
            DatePicker.SelectedDate = DateTime.Today;

            // Load departments for filter
            try
            {
                _departments = await ServiceLocator.DepartmentRepository.GetAllAsync();

                // Add "All Departments" option
                var allDepts = new List<Department> { new Department { DeptId = 0, DeptName = "جميع الأقسام" } };
                allDepts.AddRange(_departments);

                DepartmentCombo.ItemsSource = allDepts;
                DepartmentCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الأقسام:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Load initial data
            await LoadDashboardDataAsync();

            // Setup auto-refresh timer (5 minutes)
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _autoRefreshTimer.Tick += async (s, args) => await LoadDashboardDataAsync();
            _autoRefreshTimer.Start();
        }

        private async System.Threading.Tasks.Task LoadDashboardDataAsync()
        {
            if (_isLoading) return;

            _isLoading = true;
            LoadingOverlay.Visibility = Visibility.Visible;
            RefreshButton.IsEnabled = false;

            try
            {
                var selectedDate = DatePicker.SelectedDate ?? DateTime.Today;
                var departmentIds = GetSelectedDepartmentIds();

                // Load KPI data
                var kpiData = await ServiceLocator.DashboardService.GetKpiDataAsync(selectedDate, departmentIds);
                UpdateKpiCards(kpiData);

                // Load absentees
                var absentees = await ServiceLocator.DashboardService.GetAbsenteesAsync(selectedDate, departmentIds);
                AbsenteesGrid.ItemsSource = absentees;
                AbsenteesCountText.Text = $" ({absentees.Count})";

                // Load late arrivals
                var lateArrivals = await ServiceLocator.DashboardService.GetLateArrivalsAsync(selectedDate, departmentIds);
                LateArrivalsGrid.ItemsSource = lateArrivals;
                LateCountText.Text = $" ({lateArrivals.Count})";

                // Update last refresh time
                LastUpdateText.Text = $"آخر تحديث: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل بيانات لوحة المعلومات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                RefreshButton.IsEnabled = true;
                _isLoading = false;
            }
        }

        private void UpdateKpiCards(AttendanceKpiData kpiData)
        {
            TotalEmployeesText.Text = kpiData.TotalEmployees.ToString();
            PresentTodayText.Text = kpiData.PresentToday.ToString();
            AbsentTodayText.Text = kpiData.AbsentToday.ToString();
            LateArrivalsText.Text = kpiData.LateArrivals.ToString();
            AttendanceRateText.Text = $"{kpiData.AttendanceRate:F1}%";
        }

        private List<int> GetSelectedDepartmentIds()
        {
            var selectedDept = DepartmentCombo.SelectedItem as Department;
            if (selectedDept == null || selectedDept.DeptId == 0)
            {
                return null; // All departments
            }
            return new List<int> { selectedDept.DeptId };
        }

        private async void DatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                await LoadDashboardDataAsync();
            }
        }

        private async void DepartmentCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                await LoadDashboardDataAsync();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer?.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoRefreshTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
