using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Pagination;

namespace ZKTecoManager
{
    public partial class EmployeesWindow : Window
    {
        private readonly IUserRepository _userRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IShiftRepository _shiftRepository;

        private List<User> allUsers = new List<User>();
        private List<Department> allDepartments = new List<Department>();
        private bool _isLoading;

        // Pagination state
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;
        private int _totalRecords = 0;

        public EmployeesWindow()
        {
            InitializeComponent();
            _userRepository = ServiceLocator.UserRepository;
            _departmentRepository = ServiceLocator.DepartmentRepository;
            _shiftRepository = ServiceLocator.ShiftRepository;
            this.FlowDirection = (Thread.CurrentThread.CurrentUICulture.Name == "ar-IQ") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            this.KeyDown += EmployeesWindow_KeyDown;
        }

        private void EmployeesWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.N:
                        AddUser_Click(null, null);
                        e.Handled = true;
                        break;
                    case Key.E:
                        EditUser_Click(null, null);
                        e.Handled = true;
                        break;
                    case Key.F:
                        UserSearchTextBox.Focus();
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Key.Delete)
            {
                DeleteUser_Click(null, null);
                e.Handled = true;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Show("جاري تحميل البيانات...", "الرجاء الانتظار");
            try
            {
                await LoadDepartmentsIntoFilterAsync();
                await RefreshUserListAsync();
            }
            finally
            {
                LoadingOverlay.Hide();
            }
        }

        #region Window Behavior
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { this.Close(); }
        #endregion

        private async void Filters_Changed(object sender, RoutedEventArgs e)
        {
            _currentPage = 1; // Reset to first page when filters change
            await RefreshUserListAsync();
        }

        #region Bulk Assignment Logic
        private async void AssignShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var shifts = await _shiftRepository.GetAllAsync();
                var departments = allDepartments;
                var users = allUsers;

                var bulkAssignWindow = new BulkAssignShiftWindow(shifts, departments, users) { Owner = this };

                if (bulkAssignWindow.ShowDialog() == true)
                {
                    var targetShiftId = bulkAssignWindow.SelectedShift.ShiftId;
                    var userIdsToUpdate = bulkAssignWindow.SelectedUserIds;

                    var usersAffected = await _userRepository.BulkAssignShiftAsync(userIdsToUpdate, targetShiftId);

                    // Log the action
                    AuditLogger.Log("BULK_ASSIGN_SHIFT", null, null, null, null,
                        $"Assigned shift '{bulkAssignWindow.SelectedShift.ShiftName}' to {usersAffected} employees");

                    MessageBox.Show($"تم تعيين {usersAffected} موظف(ين) للوردية '{bulkAssignWindow.SelectedShift.ShiftName}'.",
                        "تم التعيين بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshUserListAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء التحديث:\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User selectedUser)
            {
                var historyWindow = new EmployeeHistoryWindow(selectedUser.BadgeNumber, selectedUser.Name) { Owner = this };
                historyWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("الرجاء تحديد موظف لعرض سجل التغييرات.", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region Grid and Filter Logic
        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox).IsChecked ?? false;
            var currentView = UsersGrid.ItemsSource as List<User>;
            if (currentView == null) return;

            foreach (var user in currentView)
            {
                user.IsSelected = isChecked;
            }

            UsersGrid.ItemsSource = null;
            UsersGrid.ItemsSource = currentView;
        }

        private void ApplyLocalFilters()
        {
            IEnumerable<User> currentView = allUsers;

            if (DepartmentFilterComboBox.SelectedItem is Department selectedDept && selectedDept.DeptId >= 0)
            {
                currentView = currentView.Where(user => user.DefaultDeptId == selectedDept.DeptId);
            }

            string searchText = UserSearchTextBox.Text?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                currentView = currentView.Where(user =>
                    (user.Name != null && user.Name.ToLower().Contains(searchText)) ||
                    (user.BadgeNumber != null && user.BadgeNumber.Contains(searchText))
                );
            }

            UsersGrid.ItemsSource = currentView.ToList();
        }

        private async System.Threading.Tasks.Task RefreshUserListAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                // Build filter parameters
                int? departmentId = null;
                if (DepartmentFilterComboBox.SelectedItem is Department selectedDept && selectedDept.DeptId >= 0)
                {
                    departmentId = selectedDept.DeptId;
                }

                string searchText = UserSearchTextBox.Text;

                var paginationParams = new PaginationParams
                {
                    Page = _currentPage,
                    PageSize = _pageSize,
                    SearchTerm = searchText,
                    DepartmentId = departmentId
                };

                var result = await _userRepository.GetPagedAsync(paginationParams);

                allUsers = result.Items;
                _totalRecords = result.TotalCount;
                _totalPages = result.TotalPages;

                UsersGrid.ItemsSource = allUsers;
                UpdatePaginationUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل الموظفين من قاعدة البيانات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdatePaginationUI()
        {
            if (PageInfoText != null)
            {
                PageInfoText.Text = $"الصفحة {_currentPage} من {_totalPages} ({_totalRecords} موظف)";
            }
            if (PreviousPageButton != null)
            {
                PreviousPageButton.IsEnabled = _currentPage > 1;
            }
            if (NextPageButton != null)
            {
                NextPageButton.IsEnabled = _currentPage < _totalPages;
            }
        }
        #endregion

        #region Pagination
        private async void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await RefreshUserListAsync();
            }
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                await RefreshUserListAsync();
            }
        }
        #endregion

        #region CRUD Methods
        private async void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var addUserWindow = new AddUserWindow { Owner = this };
            if (addUserWindow.ShowDialog() == true)
            {
                await RefreshUserListAsync();
            }
        }

        private async void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User selectedUser)
            {
                var editUserWindow = new EditUserWindow(selectedUser) { Owner = this };
                if (editUserWindow.ShowDialog() == true)
                {
                    await RefreshUserListAsync();
                }
            }
            else
            {
                MessageBox.Show("الرجاء تحديد موظف للتعديل.", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var selectedUsers = allUsers.Where(u => u.IsSelected).ToList();
            if (selectedUsers.Count == 0 && UsersGrid.SelectedItem is User singleUser)
            {
                selectedUsers.Add(singleUser);
            }

            if (selectedUsers.Count == 0)
            {
                MessageBox.Show("الرجاء تحديد موظف أو أكثر للحذف.", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"هل أنت متأكد من حذف {selectedUsers.Count} موظف(ين)؟", "تأكيد الحذف",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                LoadingOverlay.Show("جاري حذف الموظفين...", "الرجاء الانتظار");
                try
                {
                    var userIds = selectedUsers.Select(u => u.UserId).ToList();
                    await _userRepository.DeleteMultipleAsync(userIds);

                    // Log the action
                    AuditLogger.Log("DELETE_EMPLOYEES", null, null, null, null,
                        $"Deleted {selectedUsers.Count} employees: {string.Join(", ", selectedUsers.Select(u => u.Name))}");

                    await RefreshUserListAsync();
                    MessageBox.Show($"تم حذف {selectedUsers.Count} موظف(ين) بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل حذف الموظف(ين):\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    LoadingOverlay.Hide();
                }
            }
        }
        #endregion

        #region Database Methods
        private async System.Threading.Tasks.Task LoadDepartmentsIntoFilterAsync()
        {
            try
            {
                allDepartments.Clear();
                allDepartments.Add(new Department { DeptId = -1, DeptName = "جميع الأقسام" });

                List<Department> departments;
                if (CurrentUser.Role == "superadmin")
                {
                    departments = await _departmentRepository.GetAllAsync();
                }
                else
                {
                    departments = await _departmentRepository.GetAccessibleDepartmentsAsync(CurrentUser.PermittedDepartmentIds);
                }

                allDepartments.AddRange(departments);
                DepartmentFilterComboBox.ItemsSource = allDepartments;
                DepartmentFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل الأقسام:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}
