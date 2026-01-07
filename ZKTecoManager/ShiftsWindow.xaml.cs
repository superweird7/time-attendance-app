using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class ShiftsWindow : Window
    {
        private readonly IShiftRepository _shiftRepository;
        private bool _isLoading;

        public ShiftsWindow()
        {
            InitializeComponent();
            _shiftRepository = ServiceLocator.ShiftRepository;
            this.FlowDirection = (Thread.CurrentThread.CurrentUICulture.Name == "ar-IQ") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            this.Loaded += ShiftsWindow_Loaded;
            this.KeyDown += ShiftsWindow_KeyDown;
        }

        private void ShiftsWindow_KeyDown(object sender, KeyEventArgs e)
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
                        AddShift_Click(null, null);
                        e.Handled = true;
                        break;
                    case Key.E:
                        EditShift_Click(null, null);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Key.Delete)
            {
                DeleteShift_Click(null, null);
                e.Handled = true;
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

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        #region Data Operations

        private async void ShiftsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (CurrentUser.Role == "superadmin")
            {
                ActionButtonsPanel.Visibility = Visibility.Visible;
            }
            await RefreshShiftsGridAsync();
        }

        private async System.Threading.Tasks.Task RefreshShiftsGridAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                var shifts = await _shiftRepository.GetAllAsync();
                ShiftsGrid.ItemsSource = shifts;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل الورديات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async void AddShift_Click(object sender, RoutedEventArgs e)
        {
            var addShiftWindow = new AddEditShiftWindow() { Owner = this };
            if (addShiftWindow.ShowDialog() == true)
            {
                try
                {
                    var newShift = addShiftWindow.ShiftData;

                    // Check if name already exists
                    if (await _shiftRepository.ExistsAsync(newShift.ShiftName))
                    {
                        MessageBox.Show("اسم الوردية موجود مسبقاً", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await _shiftRepository.AddAsync(newShift);

                    // Log the action
                    AuditLogger.Log("ADD_SHIFT", null, null, null, null, $"Added shift: {newShift.ShiftName}");

                    await RefreshShiftsGridAsync();
                    MessageBox.Show("تم إضافة الوردية بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل حفظ الوردية:\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void EditShift_Click(object sender, RoutedEventArgs e)
        {
            if (ShiftsGrid.SelectedItem is Shift selectedShift)
            {
                try
                {
                    // Load the shift with its rules from database
                    var shiftWithRules = await _shiftRepository.GetByIdAsync(selectedShift.ShiftId);
                    if (shiftWithRules == null)
                    {
                        MessageBox.Show("لم يتم العثور على الوردية", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // If no rules were found, use the main start/end times as a default fallback
                    if (shiftWithRules.Rules.Count == 0)
                    {
                        shiftWithRules.Rules.Add(shiftWithRules.StartTime);
                        shiftWithRules.Rules.Add(shiftWithRules.EndTime);
                    }

                    var editShiftWindow = new AddEditShiftWindow(shiftWithRules) { Owner = this };
                    if (editShiftWindow.ShowDialog() == true)
                    {
                        var updatedShift = editShiftWindow.ShiftData;
                        updatedShift.ShiftId = selectedShift.ShiftId;

                        // Check if name already exists (excluding current)
                        if (await _shiftRepository.ExistsAsync(updatedShift.ShiftName, selectedShift.ShiftId))
                        {
                            MessageBox.Show("اسم الوردية موجود مسبقاً", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var oldName = selectedShift.ShiftName;
                        await _shiftRepository.UpdateAsync(updatedShift);

                        // Log the action
                        AuditLogger.Log("UPDATE_SHIFT", null, null, null, null, $"Updated shift: {oldName} -> {updatedShift.ShiftName}");

                        await RefreshShiftsGridAsync();
                        MessageBox.Show("تم تحديث الوردية بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل تحديث الوردية:\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("الرجاء تحديد وردية للتعديل", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void DeleteShift_Click(object sender, RoutedEventArgs e)
        {
            if (ShiftsGrid.SelectedItem is Shift selectedShift)
            {
                if (MessageBox.Show(
                    $"هل أنت متأكد من حذف الوردية '{selectedShift.ShiftName}'؟\n\nسيتم حذف جميع القواعد المرتبطة بهذه الوردية.",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _shiftRepository.DeleteAsync(selectedShift.ShiftId);

                        // Log the action
                        AuditLogger.Log("DELETE_SHIFT", null, null, null, null, $"Deleted shift: {selectedShift.ShiftName}");

                        MessageBox.Show("تم حذف الوردية بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                        await RefreshShiftsGridAsync();
                    }
                    catch (Npgsql.PostgresException ex) when (ex.SqlState == "23503")
                    {
                        MessageBox.Show(
                            "لا يمكن حذف هذه الوردية لأنها مخصصة حاليًا لموظف أو أكثر.\n\nالرجاء إلغاء تخصيص الوردية من الموظفين أولاً.",
                            "فشل الحذف",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"فشل حذف الوردية:\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("الرجاء تحديد وردية للحذف", "لا يوجد تحديد", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion
    }
}
