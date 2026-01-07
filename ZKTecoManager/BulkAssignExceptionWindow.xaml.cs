using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager
{
    public partial class BulkAssignExceptionWindow : Window
    {
        private readonly List<NodeViewModel> _tree;
        public ExceptionType SelectedExceptionType { get; private set; }
        public DateTime SelectedStartDate { get; private set; }
        public DateTime SelectedEndDate { get; private set; }
        public List<int> SelectedUserIds { get; private set; }
        public string Notes { get; private set; }

        public BulkAssignExceptionWindow(List<ExceptionType> exceptionTypes, List<Department> departments, List<User> users)
        {
            InitializeComponent();
            SelectedUserIds = new List<int>();

            ExceptionTypeComboBox.ItemsSource = exceptionTypes;
            if (exceptionTypes.Any()) ExceptionTypeComboBox.SelectedIndex = 0;

            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today;

            _tree = new List<NodeViewModel>();
            var allEmployeesNode = new DepartmentNodeViewModel("All Employees");

            var departmentNodeLookup = departments
                .Where(d => d.DeptId >= 0)
                .ToDictionary(d => d.DeptId, d => new DepartmentNodeViewModel(d.DeptName));

            if (!departmentNodeLookup.ContainsKey(0))
            {
                departmentNodeLookup[0] = new DepartmentNodeViewModel("Unknown");
            }

            foreach (var user in users)
            {
                if (departmentNodeLookup.TryGetValue(user.DefaultDeptId, out var deptNode))
                {
                    deptNode.Children.Add(new EmployeeNodeViewModel(user.UserId, user.Name, deptNode));
                }
                else
                {
                    departmentNodeLookup[0].Children.Add(new EmployeeNodeViewModel(user.UserId, user.Name, departmentNodeLookup[0]));
                }
            }

            foreach (var deptNode in departmentNodeLookup.Values.Where(d => d.Children.Any()).OrderBy(d => d.Name))
            {
                allEmployeesNode.Children.Add(deptNode);
            }

            _tree.Add(allEmployeesNode);
            EmployeeTreeView.ItemsSource = _tree;
        }

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

        private void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (ExceptionTypeComboBox.SelectedItem == null || !StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("الرجاء اختيار نوع الاستثناء ونطاق تاريخ صالح", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (EndDatePicker.SelectedDate.Value.Date < StartDatePicker.SelectedDate.Value.Date)
            {
                MessageBox.Show("تاريخ النهاية لا يمكن أن يكون قبل تاريخ البداية", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedExceptionType = ExceptionTypeComboBox.SelectedItem as ExceptionType;
            SelectedStartDate = StartDatePicker.SelectedDate.Value.Date;
            SelectedEndDate = EndDatePicker.SelectedDate.Value.Date;
            Notes = NotesTextBox.Text;

            SelectedUserIds.Clear();
            FindSelectedEmployees(_tree);

            if (!SelectedUserIds.Any())
            {
                MessageBox.Show("الرجاء اختيار موظف أو قسم واحد على الأقل", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
        }

        private void FindSelectedEmployees(IEnumerable<NodeViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is EmployeeNodeViewModel employeeNode && employeeNode.IsChecked == true)
                {
                    if (!SelectedUserIds.Contains(employeeNode.UserId))
                    {
                        SelectedUserIds.Add(employeeNode.UserId);
                    }
                }
                if (node.Children.Any())
                {
                    FindSelectedEmployees(node.Children);
                }
            }
        }
    }
}
