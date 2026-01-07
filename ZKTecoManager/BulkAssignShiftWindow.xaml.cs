using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager
{
    public partial class BulkAssignShiftWindow : Window
    {
        private readonly List<NodeViewModel> _tree;
        public Shift SelectedShift { get; private set; }
        public List<int> SelectedUserIds { get; private set; }

        public BulkAssignShiftWindow(List<Shift> shifts, List<Department> departments, List<User> users)
        {
            InitializeComponent();
            SelectedUserIds = new List<int>();

            ShiftComboBox.ItemsSource = shifts;
            if (shifts.Any()) ShiftComboBox.SelectedIndex = 0;

            _tree = new List<NodeViewModel>();
            var allEmployeesNode = new DepartmentNodeViewModel("All Employees");

            var realDepartments = departments.Where(d => d.DeptId > 0).ToList();
            foreach (var dept in realDepartments)
            {
                var deptNode = new DepartmentNodeViewModel(dept.DeptName);
                var usersInDept = users.Where(u => u.DefaultDeptId == dept.DeptId);
                deptNode.AddChildren(usersInDept);
                allEmployeesNode.Children.Add(deptNode);
            }

            var unassignedUsers = users.Where(u => u.DefaultDeptId == 0);
            var unassignedNode = new DepartmentNodeViewModel("Unknown");
            unassignedNode.AddChildren(unassignedUsers);
            if (unassignedNode.Children.Any()) allEmployeesNode.Children.Add(unassignedNode);

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
            if (ShiftComboBox.SelectedItem == null)
            {
                MessageBox.Show("الرجاء اختيار وردية", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedShift = ShiftComboBox.SelectedItem as Shift;

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
                    SelectedUserIds.Add(employeeNode.UserId);
                }

                if (node.Children.Any())
                {
                    FindSelectedEmployees(node.Children);
                }
            }
        }
    }
}
