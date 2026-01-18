using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager
{
    public partial class AdjustBalanceDialog : Window
    {
        private readonly User _employee;
        private readonly int _leaveTypeId;
        private readonly int _year;

        public AdjustBalanceDialog(User employee, int leaveTypeId, string leaveTypeName, int year, decimal currentBalance)
        {
            InitializeComponent();
            _employee = employee;
            _leaveTypeId = leaveTypeId;
            _year = year;

            // Set display info
            EmployeeInfoText.Text = $"{employee.BadgeNumber} - {employee.Name}";
            LeaveTypeText.Text = leaveTypeName;
            CurrentBalanceText.Text = currentBalance.ToString("N2");
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate adjustment amount
            if (!decimal.TryParse(AdjustmentTextBox.Text, out var adjustment))
            {
                MessageBox.Show("الرجاء إدخال رقم صحيح للتعديل", "خطأ في الإدخال",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AdjustmentTextBox.Focus();
                return;
            }

            if (adjustment == 0)
            {
                MessageBox.Show("مقدار التعديل يجب أن يكون مختلفاً عن صفر", "خطأ في الإدخال",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                AdjustmentTextBox.Focus();
                return;
            }

            var reason = ReasonTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show("الرجاء إدخال سبب التعديل", "خطأ في الإدخال",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ReasonTextBox.Focus();
                return;
            }

            try
            {
                var leaveService = ServiceLocator.LeaveManagementService;
                var result = await leaveService.AdjustBalanceAsync(
                    _employee.UserId,
                    _leaveTypeId,
                    _year,
                    adjustment,
                    reason,
                    CurrentUser.UserId);

                if (result.Success)
                {
                    MessageBox.Show(result.Message, "نجاح",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show(result.Message, "خطأ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ التعديل:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
