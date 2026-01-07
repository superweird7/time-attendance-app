using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class DepartmentsWindow : Window
    {
        private readonly IDepartmentRepository _departmentRepository;
        private bool _isLoading;

        public DepartmentsWindow()
        {
            InitializeComponent();
            _departmentRepository = ServiceLocator.DepartmentRepository;
            this.FlowDirection = (Thread.CurrentThread.CurrentUICulture.Name == "ar-IQ") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            this.Loaded += DepartmentsWindow_Loaded;
            this.KeyDown += DepartmentsWindow_KeyDown;
        }

        private void DepartmentsWindow_KeyDown(object sender, KeyEventArgs e)
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
                        AddDepartment_Click(null, null);
                        e.Handled = true;
                        break;
                    case Key.E:
                        EditDepartment_Click(null, null);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Key.Delete)
            {
                DeleteDepartment_Click(null, null);
                e.Handled = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private async void DepartmentsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (CurrentUser.Role == "superadmin")
            {
                ActionButtonsPanel.Visibility = Visibility.Visible;
            }
            await RefreshDepartmentsGridAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async System.Threading.Tasks.Task RefreshDepartmentsGridAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                var departments = await _departmentRepository.GetAllAsync();
                DepartmentsGrid.ItemsSource = departments;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل الأقسام: {ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async void AddDepartment_Click(object sender, RoutedEventArgs e)
        {
            var addEditWindow = new AddEditDepartmentWindow() { Owner = this };
            if (addEditWindow.ShowDialog() == true)
            {
                try
                {
                    // Check if name already exists
                    if (await _departmentRepository.ExistsAsync(addEditWindow.DepartmentName))
                    {
                        MessageBox.Show("اسم القسم موجود مسبقاً", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var headUserId = addEditWindow.SelectedHeadUserId;
                    if (headUserId == 0) headUserId = null;

                    var newDept = new Department
                    {
                        DeptName = addEditWindow.DepartmentName,
                        HeadUserId = headUserId
                    };
                    await _departmentRepository.AddAsync(newDept);

                    // Log the action
                    AuditLogger.Log("ADD_DEPARTMENT", null, null, null, null, $"Added department: {addEditWindow.DepartmentName}");

                    await RefreshDepartmentsGridAsync();
                    MessageBox.Show("تم إضافة القسم بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل إضافة القسم: {ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void EditDepartment_Click(object sender, RoutedEventArgs e)
        {
            if (DepartmentsGrid.SelectedItem is Department selectedDept)
            {
                var addEditWindow = new AddEditDepartmentWindow(selectedDept) { Owner = this };
                if (addEditWindow.ShowDialog() == true)
                {
                    try
                    {
                        // Check if name already exists (excluding current)
                        if (await _departmentRepository.ExistsAsync(addEditWindow.DepartmentName, selectedDept.DeptId))
                        {
                            MessageBox.Show("اسم القسم موجود مسبقاً", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var oldName = selectedDept.DeptName;
                        selectedDept.DeptName = addEditWindow.DepartmentName;

                        var headUserId = addEditWindow.SelectedHeadUserId;
                        if (headUserId == 0) headUserId = null;
                        selectedDept.HeadUserId = headUserId;

                        await _departmentRepository.UpdateAsync(selectedDept);

                        // Log the action
                        AuditLogger.Log("UPDATE_DEPARTMENT", null, null, null, null, $"Updated department: {oldName} -> {addEditWindow.DepartmentName}");

                        await RefreshDepartmentsGridAsync();
                        MessageBox.Show("تم تحديث القسم بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"فشل تحديث القسم: {ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("الرجاء اختيار قسم للتعديل", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DeleteDepartment_Click(object sender, RoutedEventArgs e)
        {
            if (DepartmentsGrid.SelectedItem is Department selectedDept)
            {
                if (MessageBox.Show($"هل أنت متأكد من حذف '{selectedDept.DeptName}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _departmentRepository.DeleteAsync(selectedDept.DeptId);

                        // Log the action
                        AuditLogger.Log("DELETE_DEPARTMENT", null, null, null, null, $"Deleted department: {selectedDept.DeptName}");

                        await RefreshDepartmentsGridAsync();
                        MessageBox.Show("تم حذف القسم بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Npgsql.PostgresException ex) when (ex.SqlState == "23503")
                    {
                        MessageBox.Show("لا يمكن حذف هذا القسم لأنه معين حالياً لموظف أو أكثر", "فشل الحذف", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"فشل حذف القسم: {ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("الرجاء اختيار قسم للحذف", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
