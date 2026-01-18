using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Leave;

namespace ZKTecoManager
{
    public partial class LeaveReportsWindow : Window
    {
        private List<Department> _departments;
        private List<LeaveType> _leaveTypes;
        private object _currentReportData;
        private string _currentReportType;

        public LeaveReportsWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                // Initialize year selector
                var currentYear = DateTime.Now.Year;
                var years = new List<int>();
                for (int y = currentYear - 2; y <= currentYear + 1; y++)
                {
                    years.Add(y);
                }
                YearComboBox.ItemsSource = years;
                YearComboBox.SelectedItem = currentYear;

                // Load departments
                await LoadDepartmentsAsync();

                // Load leave types
                var leaveRepo = ServiceLocator.LeaveRepository;
                _leaveTypes = await leaveRepo.GetAllLeaveTypesAsync(false);

                EmptyState.Visibility = Visibility.Visible;

                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"خطأ في تحميل البيانات:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadDepartmentsAsync()
        {
            var deptRepo = ServiceLocator.DepartmentRepository;
            var allDepts = await deptRepo.GetAllAsync();

            // Filter by permissions
            if (CurrentUser.IsSuperAdmin)
            {
                _departments = allDepts;
            }
            else if (CurrentUser.PermittedDepartmentIds.Any())
            {
                _departments = allDepts.Where(d => CurrentUser.PermittedDepartmentIds.Contains(d.DeptId)).ToList();
            }
            else
            {
                _departments = new List<Department>();
            }

            // Add "All" option
            var deptList = new List<Department> { new Department { DeptId = 0, DeptName = "جميع الأقسام" } };
            deptList.AddRange(_departments);
            DepartmentComboBox.ItemsSource = deptList;
            DepartmentComboBox.SelectedIndex = 0;
        }

        private void ReportTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against event firing during InitializeComponent
            if (MonthFilterPanel == null) return;

            var selectedItem = ReportTypeComboBox.SelectedItem as ComboBoxItem;
            var reportType = selectedItem?.Tag?.ToString();

            // Show/hide month filter based on report type
            if (reportType == "usage" || reportType == "hourly" || reportType == "all_transactions")
            {
                MonthFilterPanel.Visibility = Visibility.Visible;
            }
            else
            {
                MonthFilterPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void GenerateReportButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedReportItem = ReportTypeComboBox.SelectedItem as ComboBoxItem;
            var reportType = selectedReportItem?.Tag?.ToString();

            if (string.IsNullOrEmpty(reportType))
            {
                MessageBox.Show("الرجاء اختيار نوع التقرير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentReportType = reportType;

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;

                var selectedDept = DepartmentComboBox.SelectedItem as Department;
                var selectedYear = (int)YearComboBox.SelectedItem;
                var selectedMonthItem = MonthComboBox.SelectedItem as ComboBoxItem;
                var selectedMonth = int.Parse(selectedMonthItem?.Tag?.ToString() ?? "0");

                switch (reportType)
                {
                    case "balance_summary":
                        await GenerateBalanceSummaryReportAsync(selectedDept?.DeptId ?? 0, selectedYear);
                        break;
                    case "usage":
                        await GenerateUsageReportAsync(selectedDept?.DeptId ?? 0, selectedYear, selectedMonth);
                        break;
                    case "hourly":
                        await GenerateHourlyReportAsync(selectedDept?.DeptId ?? 0, selectedYear, selectedMonth);
                        break;
                    case "all_transactions":
                        await GenerateAllTransactionsReportAsync(selectedDept?.DeptId ?? 0, selectedYear, selectedMonth);
                        break;
                }

                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"خطأ في إنشاء التقرير:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task GenerateBalanceSummaryReportAsync(int departmentId, int year)
        {
            ReportTitleText.Text = "ملخص أرصدة الإجازات";
            ReportSubtitleText.Text = $"السنة: {year}";

            var userRepo = ServiceLocator.UserRepository;
            var leaveRepo = ServiceLocator.LeaveRepository;

            // Get employees
            var allUsers = await userRepo.GetAllAsync();
            if (departmentId > 0)
            {
                allUsers = allUsers.Where(u => u.DefaultDeptId == departmentId).ToList();
            }
            else if (!CurrentUser.IsSuperAdmin && CurrentUser.PermittedDepartmentIds.Any())
            {
                allUsers = allUsers.Where(u =>
                    CurrentUser.PermittedDepartmentIds.Contains(u.DefaultDeptId)).ToList();
            }

            var reportData = new List<BalanceSummaryRow>();

            foreach (var user in allUsers.OrderBy(u => u.BadgeNumber))
            {
                var balances = await leaveRepo.GetBalancesByUserAsync(user.UserId, year);

                var row = new BalanceSummaryRow
                {
                    BadgeNumber = user.BadgeNumber,
                    EmployeeName = user.Name,
                    DepartmentName = user.Departments ?? "-",
                    OrdinaryRemaining = balances.FirstOrDefault(b => b.LeaveTypeCode == "ORDINARY")?.RemainingDays ?? 0,
                    SickFullRemaining = balances.FirstOrDefault(b => b.LeaveTypeCode == "SICK_FULL")?.RemainingDays ?? 0,
                    SickHalfRemaining = balances.FirstOrDefault(b => b.LeaveTypeCode == "SICK_HALF")?.RemainingDays ?? 0
                };

                reportData.Add(row);
            }

            _currentReportData = reportData;

            // Configure columns
            ReportDataGrid.Columns.Clear();
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "رقم الموظف", Binding = new Binding("BadgeNumber"), Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "اسم الموظف", Binding = new Binding("EmployeeName"), Width = 180 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "القسم", Binding = new Binding("DepartmentName"), Width = 150 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "الاعتيادية", Binding = new Binding("OrdinaryRemaining") { StringFormat = "N2" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "مرضية كامل", Binding = new Binding("SickFullRemaining") { StringFormat = "N2" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "مرضية نصف", Binding = new Binding("SickHalfRemaining") { StringFormat = "N2" }, Width = 100 });

            ReportDataGrid.ItemsSource = reportData;
        }

        private async Task GenerateUsageReportAsync(int departmentId, int year, int month)
        {
            ReportTitleText.Text = "تقرير استخدام الإجازات";
            ReportSubtitleText.Text = month > 0 ? $"السنة: {year} - الشهر: {month}" : $"السنة: {year}";

            var userRepo = ServiceLocator.UserRepository;
            var leaveRepo = ServiceLocator.LeaveRepository;

            var allUsers = await userRepo.GetAllAsync();
            if (departmentId > 0)
            {
                allUsers = allUsers.Where(u => u.DefaultDeptId == departmentId).ToList();
            }
            else if (!CurrentUser.IsSuperAdmin && CurrentUser.PermittedDepartmentIds.Any())
            {
                allUsers = allUsers.Where(u =>
                    CurrentUser.PermittedDepartmentIds.Contains(u.DefaultDeptId)).ToList();
            }

            var reportData = new List<UsageReportRow>();

            DateTime? startDate = month > 0 ? new DateTime(year, month, 1) : new DateTime(year, 1, 1);
            DateTime? endDate = month > 0 ? new DateTime(year, month, DateTime.DaysInMonth(year, month)) : new DateTime(year, 12, 31);

            // Get leave type IDs for filtering
            var ordinaryTypeId = _leaveTypes.FirstOrDefault(lt => lt.LeaveTypeCode == "ORDINARY")?.LeaveTypeId ?? 0;
            var sickFullTypeId = _leaveTypes.FirstOrDefault(lt => lt.LeaveTypeCode == "SICK_FULL")?.LeaveTypeId ?? 0;
            var sickHalfTypeId = _leaveTypes.FirstOrDefault(lt => lt.LeaveTypeCode == "SICK_HALF")?.LeaveTypeId ?? 0;
            var unpaidTypeId = _leaveTypes.FirstOrDefault(lt => lt.LeaveTypeCode == "UNPAID")?.LeaveTypeId ?? 0;

            foreach (var user in allUsers.OrderBy(u => u.BadgeNumber))
            {
                var transactions = await leaveRepo.GetTransactionsByUserAsync(user.UserId, startDate, endDate);
                var deductions = transactions.Where(t => t.TransactionType == "deduction").ToList();

                if (!deductions.Any()) continue;

                var row = new UsageReportRow
                {
                    BadgeNumber = user.BadgeNumber,
                    EmployeeName = user.Name,
                    OrdinaryUsed = deductions.Where(d => d.LeaveTypeId == ordinaryTypeId).Sum(d => d.DaysAmount),
                    SickFullUsed = deductions.Where(d => d.LeaveTypeId == sickFullTypeId).Sum(d => d.DaysAmount),
                    SickHalfUsed = deductions.Where(d => d.LeaveTypeId == sickHalfTypeId).Sum(d => d.DaysAmount),
                    UnpaidUsed = deductions.Where(d => d.LeaveTypeId == unpaidTypeId).Sum(d => d.DaysAmount),
                    TotalUsed = deductions.Sum(d => d.DaysAmount)
                };

                reportData.Add(row);
            }

            _currentReportData = reportData;

            ReportDataGrid.Columns.Clear();
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "رقم الموظف", Binding = new Binding("BadgeNumber"), Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "اسم الموظف", Binding = new Binding("EmployeeName"), Width = 180 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "الاعتيادية", Binding = new Binding("OrdinaryUsed") { StringFormat = "N2" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "مرضية كامل", Binding = new Binding("SickFullUsed") { StringFormat = "N2" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "مرضية نصف", Binding = new Binding("SickHalfUsed") { StringFormat = "N2" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "بدون راتب", Binding = new Binding("UnpaidUsed") { StringFormat = "N2" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "المجموع", Binding = new Binding("TotalUsed") { StringFormat = "N2" }, Width = 100 });

            ReportDataGrid.ItemsSource = reportData;
        }

        private async Task GenerateHourlyReportAsync(int departmentId, int year, int month)
        {
            ReportTitleText.Text = "تقرير الإجازات الساعية";
            ReportSubtitleText.Text = month > 0 ? $"السنة: {year} - الشهر: {month}" : $"السنة: {year}";

            var userRepo = ServiceLocator.UserRepository;
            var leaveRepo = ServiceLocator.LeaveRepository;

            var allUsers = await userRepo.GetAllAsync();
            if (departmentId > 0)
            {
                allUsers = allUsers.Where(u => u.DefaultDeptId == departmentId).ToList();
            }
            else if (!CurrentUser.IsSuperAdmin && CurrentUser.PermittedDepartmentIds.Any())
            {
                allUsers = allUsers.Where(u =>
                    CurrentUser.PermittedDepartmentIds.Contains(u.DefaultDeptId)).ToList();
            }

            DateTime? startDate = month > 0 ? new DateTime(year, month, 1) : new DateTime(year, 1, 1);
            DateTime? endDate = month > 0 ? new DateTime(year, month, DateTime.DaysInMonth(year, month)) : new DateTime(year, 12, 31);

            var reportData = new List<HourlyReportRow>();

            foreach (var user in allUsers.OrderBy(u => u.BadgeNumber))
            {
                var transactions = await leaveRepo.GetTransactionsByUserAsync(user.UserId, startDate, endDate);
                var hourlyTransactions = transactions.Where(t =>
                    t.TransactionType == "hourly_conversion" || t.HoursAmount.HasValue && t.HoursAmount > 0).ToList();

                if (!hourlyTransactions.Any()) continue;

                var hourlyAcc = await leaveRepo.GetHourlyAccumulatorAsync(user.UserId);

                var row = new HourlyReportRow
                {
                    BadgeNumber = user.BadgeNumber,
                    EmployeeName = user.Name,
                    TotalHours = hourlyTransactions.Sum(t => t.HoursAmount ?? 0),
                    DaysConverted = hourlyTransactions.Where(t => t.TransactionType == "hourly_conversion").Sum(t => t.DaysAmount),
                    CurrentAccumulated = hourlyAcc?.AccumulatedHours ?? 0
                };

                reportData.Add(row);
            }

            _currentReportData = reportData;

            ReportDataGrid.Columns.Clear();
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "رقم الموظف", Binding = new Binding("BadgeNumber"), Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "اسم الموظف", Binding = new Binding("EmployeeName"), Width = 180 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "إجمالي الساعات", Binding = new Binding("TotalHours") { StringFormat = "N2" }, Width = 120 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "أيام محولة", Binding = new Binding("DaysConverted") { StringFormat = "N0" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "المتراكم حالياً", Binding = new Binding("CurrentAccumulated") { StringFormat = "N2" }, Width = 120 });

            ReportDataGrid.ItemsSource = reportData;
        }

        private async Task GenerateAllTransactionsReportAsync(int departmentId, int year, int month)
        {
            ReportTitleText.Text = "كل عمليات الإجازات";
            ReportSubtitleText.Text = month > 0 ? $"السنة: {year} - الشهر: {month}" : $"السنة: {year}";

            var userRepo = ServiceLocator.UserRepository;
            var leaveRepo = ServiceLocator.LeaveRepository;

            var allUsers = await userRepo.GetAllAsync();
            if (departmentId > 0)
            {
                allUsers = allUsers.Where(u => u.DefaultDeptId == departmentId).ToList();
            }
            else if (!CurrentUser.IsSuperAdmin && CurrentUser.PermittedDepartmentIds.Any())
            {
                allUsers = allUsers.Where(u =>
                    CurrentUser.PermittedDepartmentIds.Contains(u.DefaultDeptId)).ToList();
            }

            DateTime? startDate = month > 0 ? new DateTime(year, month, 1) : new DateTime(year, 1, 1);
            DateTime? endDate = month > 0 ? new DateTime(year, month, DateTime.DaysInMonth(year, month)) : new DateTime(year, 12, 31);

            var reportData = new List<TransactionReportRow>();

            foreach (var user in allUsers)
            {
                var transactions = await leaveRepo.GetTransactionsByUserAsync(user.UserId, startDate, endDate);

                foreach (var t in transactions)
                {
                    var leaveType = _leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == t.LeaveTypeId);

                    reportData.Add(new TransactionReportRow
                    {
                        Date = t.SubmissionDate,
                        BadgeNumber = user.BadgeNumber,
                        EmployeeName = user.Name,
                        LeaveType = leaveType?.LeaveTypeNameAr ?? "-",
                        TransactionType = GetTransactionTypeDisplay(t.TransactionType),
                        Days = t.DaysAmount,
                        Hours = t.HoursAmount,
                        Reason = t.Reason
                    });
                }
            }

            _currentReportData = reportData.OrderByDescending(r => r.Date).ToList();

            ReportDataGrid.Columns.Clear();
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "التاريخ", Binding = new Binding("Date") { StringFormat = "yyyy-MM-dd" }, Width = 100 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "رقم الموظف", Binding = new Binding("BadgeNumber"), Width = 90 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "اسم الموظف", Binding = new Binding("EmployeeName"), Width = 150 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "نوع الإجازة", Binding = new Binding("LeaveType"), Width = 120 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "العملية", Binding = new Binding("TransactionType"), Width = 80 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "الأيام", Binding = new Binding("Days") { StringFormat = "N2" }, Width = 70 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "الساعات", Binding = new Binding("Hours") { StringFormat = "N2" }, Width = 70 });
            ReportDataGrid.Columns.Add(new DataGridTextColumn { Header = "السبب", Binding = new Binding("Reason"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            ReportDataGrid.ItemsSource = _currentReportData as System.Collections.IEnumerable;
        }

        private string GetTransactionTypeDisplay(string transactionType)
        {
            switch (transactionType?.ToLower())
            {
                case "deduction": return "خصم";
                case "accrual": return "استحقاق";
                case "adjustment": return "تعديل";
                case "carryover": return "ترحيل";
                case "hourly_conversion": return "ساعي";
                case "reset": return "إعادة تعيين";
                default: return transactionType ?? "";
            }
        }

        private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentReportData == null)
            {
                MessageBox.Show("الرجاء إنشاء التقرير أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"LeaveReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                Title = "حفظ التقرير كملف PDF"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToPdf(saveDialog.FileName);
                    MessageBox.Show("تم حفظ التقرير بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Open the file
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في حفظ التقرير:\n{ex.Message}", "خطأ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportToPdf(string filePath)
        {
            using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4.Rotate(), 36, 36, 36, 36);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, fs);

                document.Open();

                // Add title
                var titleFont = iTextSharp.text.FontFactory.GetFont("Arial", iTextSharp.text.pdf.BaseFont.IDENTITY_H, true, 18, iTextSharp.text.Font.BOLD);
                var paragraph = new iTextSharp.text.Paragraph(ReportTitleText.Text, titleFont);
                paragraph.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                document.Add(paragraph);

                if (!string.IsNullOrEmpty(ReportSubtitleText.Text))
                {
                    var subtitleFont = iTextSharp.text.FontFactory.GetFont("Arial", iTextSharp.text.pdf.BaseFont.IDENTITY_H, true, 12);
                    var subtitle = new iTextSharp.text.Paragraph(ReportSubtitleText.Text, subtitleFont);
                    subtitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                    document.Add(subtitle);
                }

                document.Add(new iTextSharp.text.Paragraph(" "));

                // Create table based on report type
                var headerFont = iTextSharp.text.FontFactory.GetFont("Arial", iTextSharp.text.pdf.BaseFont.IDENTITY_H, true, 10, iTextSharp.text.Font.BOLD);
                var cellFont = iTextSharp.text.FontFactory.GetFont("Arial", iTextSharp.text.pdf.BaseFont.IDENTITY_H, true, 9);

                iTextSharp.text.pdf.PdfPTable table = null;

                switch (_currentReportType)
                {
                    case "balance_summary":
                        table = new iTextSharp.text.pdf.PdfPTable(6);
                        table.SetWidths(new float[] { 15, 25, 20, 13, 13, 13 });
                        AddPdfCell(table, "رقم الموظف", headerFont);
                        AddPdfCell(table, "اسم الموظف", headerFont);
                        AddPdfCell(table, "القسم", headerFont);
                        AddPdfCell(table, "الاعتيادية", headerFont);
                        AddPdfCell(table, "مرضية كامل", headerFont);
                        AddPdfCell(table, "مرضية نصف", headerFont);

                        foreach (var row in (List<BalanceSummaryRow>)_currentReportData)
                        {
                            AddPdfCell(table, row.BadgeNumber, cellFont);
                            AddPdfCell(table, row.EmployeeName, cellFont);
                            AddPdfCell(table, row.DepartmentName, cellFont);
                            AddPdfCell(table, row.OrdinaryRemaining.ToString("N2"), cellFont);
                            AddPdfCell(table, row.SickFullRemaining.ToString("N2"), cellFont);
                            AddPdfCell(table, row.SickHalfRemaining.ToString("N2"), cellFont);
                        }
                        break;

                    case "usage":
                        table = new iTextSharp.text.pdf.PdfPTable(7);
                        table.SetWidths(new float[] { 13, 22, 13, 13, 13, 13, 13 });
                        AddPdfCell(table, "رقم الموظف", headerFont);
                        AddPdfCell(table, "اسم الموظف", headerFont);
                        AddPdfCell(table, "الاعتيادية", headerFont);
                        AddPdfCell(table, "مرضية كامل", headerFont);
                        AddPdfCell(table, "مرضية نصف", headerFont);
                        AddPdfCell(table, "بدون راتب", headerFont);
                        AddPdfCell(table, "المجموع", headerFont);

                        foreach (var row in (List<UsageReportRow>)_currentReportData)
                        {
                            AddPdfCell(table, row.BadgeNumber, cellFont);
                            AddPdfCell(table, row.EmployeeName, cellFont);
                            AddPdfCell(table, row.OrdinaryUsed.ToString("N2"), cellFont);
                            AddPdfCell(table, row.SickFullUsed.ToString("N2"), cellFont);
                            AddPdfCell(table, row.SickHalfUsed.ToString("N2"), cellFont);
                            AddPdfCell(table, row.UnpaidUsed.ToString("N2"), cellFont);
                            AddPdfCell(table, row.TotalUsed.ToString("N2"), cellFont);
                        }
                        break;

                    case "hourly":
                        table = new iTextSharp.text.pdf.PdfPTable(5);
                        table.SetWidths(new float[] { 15, 30, 18, 18, 18 });
                        AddPdfCell(table, "رقم الموظف", headerFont);
                        AddPdfCell(table, "اسم الموظف", headerFont);
                        AddPdfCell(table, "إجمالي الساعات", headerFont);
                        AddPdfCell(table, "أيام محولة", headerFont);
                        AddPdfCell(table, "المتراكم حالياً", headerFont);

                        foreach (var row in (List<HourlyReportRow>)_currentReportData)
                        {
                            AddPdfCell(table, row.BadgeNumber, cellFont);
                            AddPdfCell(table, row.EmployeeName, cellFont);
                            AddPdfCell(table, row.TotalHours.ToString("N2"), cellFont);
                            AddPdfCell(table, row.DaysConverted.ToString("N0"), cellFont);
                            AddPdfCell(table, row.CurrentAccumulated.ToString("N2"), cellFont);
                        }
                        break;

                    case "all_transactions":
                        table = new iTextSharp.text.pdf.PdfPTable(8);
                        table.SetWidths(new float[] { 12, 10, 18, 15, 10, 8, 8, 19 });
                        AddPdfCell(table, "التاريخ", headerFont);
                        AddPdfCell(table, "رقم الموظف", headerFont);
                        AddPdfCell(table, "اسم الموظف", headerFont);
                        AddPdfCell(table, "نوع الإجازة", headerFont);
                        AddPdfCell(table, "العملية", headerFont);
                        AddPdfCell(table, "الأيام", headerFont);
                        AddPdfCell(table, "الساعات", headerFont);
                        AddPdfCell(table, "السبب", headerFont);

                        foreach (var row in (List<TransactionReportRow>)_currentReportData)
                        {
                            AddPdfCell(table, row.Date.ToString("yyyy-MM-dd"), cellFont);
                            AddPdfCell(table, row.BadgeNumber, cellFont);
                            AddPdfCell(table, row.EmployeeName, cellFont);
                            AddPdfCell(table, row.LeaveType, cellFont);
                            AddPdfCell(table, row.TransactionType, cellFont);
                            AddPdfCell(table, row.Days.ToString("N2"), cellFont);
                            AddPdfCell(table, row.Hours?.ToString("N2") ?? "-", cellFont);
                            AddPdfCell(table, row.Reason ?? "", cellFont);
                        }
                        break;
                }

                if (table != null)
                {
                    table.WidthPercentage = 100;
                    table.RunDirection = iTextSharp.text.pdf.PdfWriter.RUN_DIRECTION_RTL;
                    document.Add(table);
                }

                // Add footer
                document.Add(new iTextSharp.text.Paragraph(" "));
                var footerFont = iTextSharp.text.FontFactory.GetFont("Arial", iTextSharp.text.pdf.BaseFont.IDENTITY_H, true, 8);
                var footer = new iTextSharp.text.Paragraph($"تم الإنشاء: {DateTime.Now:yyyy-MM-dd HH:mm}", footerFont);
                footer.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                document.Add(footer);

                document.Close();
            }
        }

        private void AddPdfCell(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font)
        {
            var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(text, font));
            cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
            cell.Padding = 5;
            cell.RunDirection = iTextSharp.text.pdf.PdfWriter.RUN_DIRECTION_RTL;
            table.AddCell(cell);
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

    #region Report Row Classes

    public class BalanceSummaryRow
    {
        public string BadgeNumber { get; set; }
        public string EmployeeName { get; set; }
        public string DepartmentName { get; set; }
        public decimal OrdinaryRemaining { get; set; }
        public decimal SickFullRemaining { get; set; }
        public decimal SickHalfRemaining { get; set; }
    }

    public class UsageReportRow
    {
        public string BadgeNumber { get; set; }
        public string EmployeeName { get; set; }
        public decimal OrdinaryUsed { get; set; }
        public decimal SickFullUsed { get; set; }
        public decimal SickHalfUsed { get; set; }
        public decimal UnpaidUsed { get; set; }
        public decimal TotalUsed { get; set; }
    }

    public class HourlyReportRow
    {
        public string BadgeNumber { get; set; }
        public string EmployeeName { get; set; }
        public decimal TotalHours { get; set; }
        public decimal DaysConverted { get; set; }
        public decimal CurrentAccumulated { get; set; }
    }

    public class TransactionReportRow
    {
        public DateTime Date { get; set; }
        public string BadgeNumber { get; set; }
        public string EmployeeName { get; set; }
        public string LeaveType { get; set; }
        public string TransactionType { get; set; }
        public decimal Days { get; set; }
        public decimal? Hours { get; set; }
        public string Reason { get; set; }
    }

    #endregion
}
