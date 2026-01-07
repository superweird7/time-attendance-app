using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Printing;
using ZKTecoManager.Infrastructure;

using Itext = iTextSharp.text;

namespace ZKTecoManager
{
    public partial class ReportsWindow : Window
    {
     private List<ExceptionType> allExceptionTypes = new List<ExceptionType>();
        private List<Department> allDepartments = new List<Department>();
        private List<User> allEmployees = new List<User>();

        public ReportsWindow()
        {
            InitializeComponent();
            this.Loaded += ReportsWindow_Loaded;
        }

        private void ReportsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllExceptionTypes();
            LoadDepartments();
            LoadEmployees();
            // Set to current month by default
            var today = DateTime.Today;
            StartDatePicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
            EndDatePicker.SelectedDate = today;

            // ✅ Show manual edit button only for authorized users
            if (CurrentUser.CanEditTimes)
            {
                ManualEditButton.Visibility = Visibility.Visible;
            }
        }

        // ✅ NEW: Manual Edit Button Handler
        private void ManualEdit_Click(object sender, RoutedEventArgs e)
        {
            if (ReportDataGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("الرجاء اختيار السجلات التي تريد تعديلها", "لا يوجد تحديد",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedEntries = ReportDataGrid.SelectedItems.Cast<DailyReportEntry>().ToList();

            var manualEditWindow = new ManualTimeEditWindow(selectedEntries) { Owner = this };
            if (manualEditWindow.ShowDialog() == true)
            {
                // ✅ NEW: For each edited entry, check if we should clear the absence exception
                foreach (var entry in selectedEntries)
                {
                    bool hasClockIn = !string.IsNullOrWhiteSpace(entry.ActualClockInString) &&
                                      entry.ActualClockInString != "N/A";
                    bool hasClockOut = !string.IsNullOrWhiteSpace(entry.ActualClockOutString) &&
                                       entry.ActualClockOutString != "N/A";

                    // If both times are present, clear absence exception
                    if (hasClockIn && hasClockOut)
                    {
                        var exceptionName = entry.SelectedExceptionName?.ToLower() ?? "";
                        if (exceptionName.Contains("غياب") || exceptionName.Contains("absence"))
                        {
                            // Find the "no exception" option (usually ID = 0 or null)
                            var noException = entry.AvailableExceptions?.FirstOrDefault(e =>
                                e.ExceptionName == "لا يوجد" || e.ExceptionTypeId == 0);

                            if (noException != null)
                            {
                                entry.SelectedExceptionTypeId = noException.ExceptionTypeId;
                            }
                        }
                    }
                }

                // Regenerate report to load fresh data
                GenerateReport_Click(null, null);

                MessageBox.Show("تم تحديث التقرير بالأوقات الجديدة", "تم التحديث",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        #region Data Loading
        private void LoadAllExceptionTypes()
        {
            allExceptionTypes.Add(new ExceptionType { ExceptionTypeId = 0, ExceptionName = "No Exception" });
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sql = "SELECT exception_type_id, exception_name FROM exception_types ORDER BY exception_name";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allExceptionTypes.Add(new ExceptionType { ExceptionTypeId = reader.GetInt32(0), ExceptionName = reader.GetString(1) });
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Failed to load exception types: {ex.Message}"); }
        }

        private void LoadDepartments()
        {
            try
            {
                allDepartments.Clear();
                allDepartments.Add(new Department { DeptId = -1, DeptName = "All Departments" });
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand();

                    if (CurrentUser.Role == "superadmin")
                    {
                        cmd.CommandText = "SELECT dept_id, dept_name FROM departments ORDER BY dept_name";
                    }
                    else
                    {
                        if (CurrentUser.PermittedDepartmentIds.Count == 0) return;
                        cmd.CommandText = "SELECT dept_id, dept_name FROM departments WHERE dept_id = ANY(@permittedIds) ORDER BY dept_name";
                        cmd.Parameters.AddWithValue("permittedIds", CurrentUser.PermittedDepartmentIds);
                    }
                    cmd.Connection = conn;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allDepartments.Add(new Department { DeptId = reader.GetInt32(0), DeptName = reader.GetString(1) });
                        }
                    }
                }
                DepartmentFilterComboBox.ItemsSource = allDepartments;
                DepartmentFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex) { MessageBox.Show($"Failed to load departments: {ex.Message}"); }
        }

        private void LoadEmployees(int departmentId = -1)
        {
            try
            {
                allEmployees.Clear();
                allEmployees.Add(new User { UserId = -1, Name = "All Employees" });
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();
                    var sqlBuilder = new StringBuilder("SELECT user_id, name, badge_number, default_dept_id, shift_id FROM users ");
                    var cmd = new NpgsqlCommand();

                    if (CurrentUser.Role == "deptadmin")
                    {
                        sqlBuilder.Append("WHERE default_dept_id = ANY(@permittedIds) ");
                        cmd.Parameters.AddWithValue("permittedIds", CurrentUser.PermittedDepartmentIds);
                        if (departmentId > 0 && CurrentUser.PermittedDepartmentIds.Contains(departmentId))
                        {
                            sqlBuilder.Append("AND default_dept_id = @deptId ");
                            cmd.Parameters.AddWithValue("deptId", departmentId);
                        }
                    }
                    else if (departmentId > 0)
                    {
                        sqlBuilder.Append("WHERE default_dept_id = @deptId ");
                        cmd.Parameters.AddWithValue("deptId", departmentId);
                    }

                    sqlBuilder.Append("ORDER BY name");
                    cmd.CommandText = sqlBuilder.ToString();
                    cmd.Connection = conn;

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allEmployees.Add(new User
                            {
                                UserId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                BadgeNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                DefaultDeptId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                ShiftId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                            });
                        }
                    }
                }

                // ✅ FIXED: Force proper refresh
                EmployeeFilterComboBox.ItemsSource = null;
                EmployeeFilterComboBox.ItemsSource = allEmployees;
                EmployeeFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex) { MessageBox.Show($"Failed to load employees: {ex.Message}"); }
        }
        #endregion

        #region UI Events
        private void DepartmentFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DepartmentFilterComboBox.SelectedItem is Department selectedDept)
            {
                LoadEmployees(selectedDept.DeptId);

                // ✅ Force the ComboBox to refresh and reset selection
                EmployeeFilterComboBox.ItemsSource = null;
                EmployeeFilterComboBox.ItemsSource = allEmployees;
                EmployeeFilterComboBox.SelectedIndex = 0;
            }
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select a valid date range.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadingOverlay.Show("جاري تحميل التقرير...", "الرجاء الانتظار");
            this.Cursor = Cursors.Wait;

            AutoCleanupAbsenceExceptions();

            try
            {
                var startDate = StartDatePicker.SelectedDate.Value.Date;
                var endDate = EndDatePicker.SelectedDate.Value.Date;
                int departmentId = (DepartmentFilterComboBox.SelectedItem as Department)?.DeptId ?? -1;
                int employeeId = (EmployeeFilterComboBox.SelectedItem as User)?.UserId ?? -1;
                bool autoCalculate = AutoCalculateExceptionsCheckBox.IsChecked == true;

                var dailyReports = await Task.Run(() =>
                {
                    var allLogs = FetchRawLogs(startDate, endDate, departmentId, employeeId);
                    var existingExceptions = FetchEmployeeExceptions(startDate, endDate, departmentId, employeeId);

                    return ProcessLogsIntoDailySummary(allLogs, existingExceptions, startDate, endDate, autoCalculate, departmentId, employeeId);
                });
                ReportDataGrid.ItemsSource = null; // ✅ Clear first
                ReportDataGrid.ItemsSource = dailyReports; // ✅ Then set
                ReportDataGrid.Items.Refresh(); // ✅ Force refresh
                MessageBox.Show($"{dailyReports.Count} daily records found.", "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);

                // ✅ DEBUG: Check entries for badge 8000495
                var karrarEntries = dailyReports.Where(r => r.BadgeNumber == "8000495" && r.Date.Date == new DateTime(2025, 10, 13)).ToList();
                System.Diagnostics.Debug.WriteLine($"\n=== FINAL REPORT CHECK ===");
                System.Diagnostics.Debug.WriteLine($"Total entries for Karrar on 2025-10-13: {karrarEntries.Count}");
                foreach (var entry in karrarEntries)  // ✅ Changed 'e' to 'entry'
                {
                    System.Diagnostics.Debug.WriteLine($"  Date: {entry.Date:yyyy-MM-dd}");
                    System.Diagnostics.Debug.WriteLine($"  Clock In: {entry.ActualClockInString}");
                    System.Diagnostics.Debug.WriteLine($"  Clock Out: {entry.ActualClockOutString}");
                    System.Diagnostics.Debug.WriteLine($"  All Clocks: {entry.AllClocks}");
                    System.Diagnostics.Debug.WriteLine($"  Exception: {entry.SelectedExceptionName}");
                }

                ReportDataGrid.ItemsSource = dailyReports;
                MessageBox.Show($"{dailyReports.Count} daily records found.", "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);

                // Audit log the report generation
                string deptName = (DepartmentFilterComboBox.SelectedItem as Department)?.DeptName ?? "All";
                string empName = (EmployeeFilterComboBox.SelectedItem as User)?.Name ?? "All";
                AuditLogger.Log(
                    "REPORT",
                    "daily_reports",
                    null,
                    null,
                    $"Report Type: Daily Attendance, From: {startDate:yyyy-MM-dd}, To: {endDate:yyyy-MM-dd}, Department: {deptName}, Employee: {empName}, Records: {dailyReports.Count}",
                    $"Generated Daily Attendance report for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}, Department: {deptName}, Employee: {empName}, {dailyReports.Count} records"
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Hide();
                this.Cursor = Cursors.Arrow;
            }
        }

        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            var modifiedEntries = (ReportDataGrid.ItemsSource as List<DailyReportEntry>)?.Where(r => r.IsModified).ToList();

            if (modifiedEntries == null || !modifiedEntries.Any())
            {
                MessageBox.Show("No changes to save.", "Save Changes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            LoadingOverlay.Show("جاري حفظ التغييرات...", "الرجاء الانتظار");
            this.Cursor = Cursors.Wait;

            try
            {
                // Store changes for audit logging (must be done on UI thread to access allExceptionTypes)
                var changesForAudit = new List<(int UserId, string EmployeeName, DateTime Date, string OldValue, string NewValue)>();

                await Task.Run(() =>
                {
                    using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();
                        foreach (var entry in modifiedEntries)
                        {
                            TimeSpan? clockInOverride = null;
                            if (!string.IsNullOrWhiteSpace(entry.ActualClockInString) && TimeSpan.TryParse(entry.ActualClockInString, out var parsedIn))
                            {
                                clockInOverride = parsedIn;
                            }

                            TimeSpan? clockOutOverride = null;
                            if (!string.IsNullOrWhiteSpace(entry.ActualClockOutString) && TimeSpan.TryParse(entry.ActualClockOutString, out var parsedOut))
                            {
                                clockOutOverride = parsedOut;
                            }

                            // Fetch old exception values before deleting (for audit log)
                            string oldExceptionName = "";
                            string oldNotes = "";
                            string oldClockIn = "";
                            string oldClockOut = "";

                            var fetchOldSql = @"SELECT et.exception_name, ee.notes, ee.clock_in_override, ee.clock_out_override
                                FROM employee_exceptions ee
                                LEFT JOIN exception_types et ON ee.exception_type_id_fk = et.exception_type_id
                                WHERE ee.user_id_fk = @userId AND ee.exception_date = @date";
                            using (var fetchCmd = new NpgsqlCommand(fetchOldSql, conn))
                            {
                                fetchCmd.Parameters.AddWithValue("userId", entry.UserId);
                                fetchCmd.Parameters.AddWithValue("date", entry.Date);
                                using (var reader = fetchCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        oldExceptionName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                        oldNotes = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                        oldClockIn = reader.IsDBNull(2) ? "" : reader.GetTimeSpan(2).ToString(@"hh\:mm\:ss");
                                        oldClockOut = reader.IsDBNull(3) ? "" : reader.GetTimeSpan(3).ToString(@"hh\:mm\:ss");
                                    }
                                }
                            }

                            // Delete existing exception first
                            var deleteSql = "DELETE FROM employee_exceptions WHERE user_id_fk = @userId AND exception_date = @date";
                            using (var delCmd = new NpgsqlCommand(deleteSql, conn))
                            {
                                delCmd.Parameters.AddWithValue("userId", entry.UserId);
                                delCmd.Parameters.AddWithValue("date", entry.Date);
                                delCmd.ExecuteNonQuery();
                            }

                            // ✅ FIXED: Only insert if there's an actual exception selected (not 0) OR there are overrides/notes
                            if (entry.SelectedExceptionTypeId > 0 || !string.IsNullOrWhiteSpace(entry.Notes) || clockInOverride.HasValue || clockOutOverride.HasValue)
                            {
                                // ✅ FIXED: If exception type is 0 (No Exception), use NULL instead
                                var insertSql = @"INSERT INTO employee_exceptions
                            (user_id_fk, exception_type_id_fk, exception_date, notes, clock_in_override, clock_out_override)
                            VALUES (@userId, @exceptionId, @date, @notes, @inOverride, @outOverride)";

                                using (var insCmd = new NpgsqlCommand(insertSql, conn))
                                {
                                    insCmd.Parameters.AddWithValue("userId", entry.UserId);

                                    // ✅ FIXED: Use DBNull if exception type is 0 or negative
                                    if (entry.SelectedExceptionTypeId > 0)
                                    {
                                        insCmd.Parameters.AddWithValue("exceptionId", entry.SelectedExceptionTypeId);
                                    }
                                    else
                                    {
                                        insCmd.Parameters.AddWithValue("exceptionId", DBNull.Value);
                                    }

                                    insCmd.Parameters.AddWithValue("date", entry.Date);
                                    insCmd.Parameters.AddWithValue("notes", (object)entry.Notes ?? DBNull.Value);
                                    insCmd.Parameters.AddWithValue("inOverride", (object)clockInOverride ?? DBNull.Value);
                                    insCmd.Parameters.AddWithValue("outOverride", (object)clockOutOverride ?? DBNull.Value);
                                    insCmd.ExecuteNonQuery();
                                }
                            }

                            // Build old and new values for audit log
                            var oldValue = string.IsNullOrEmpty(oldExceptionName) ? "لا يوجد استثناء" :
                                $"استثناء: {oldExceptionName}" +
                                (string.IsNullOrEmpty(oldClockIn) ? "" : $", دخول: {oldClockIn}") +
                                (string.IsNullOrEmpty(oldClockOut) ? "" : $", خروج: {oldClockOut}") +
                                (string.IsNullOrEmpty(oldNotes) ? "" : $", ملاحظات: {oldNotes}");

                            // Get new exception name
                            string newExceptionName = "";
                            if (entry.SelectedExceptionTypeId > 0)
                            {
                                var getExNameSql = "SELECT exception_name FROM exception_types WHERE exception_type_id = @id";
                                using (var getCmd = new NpgsqlCommand(getExNameSql, conn))
                                {
                                    getCmd.Parameters.AddWithValue("id", entry.SelectedExceptionTypeId);
                                    var result = getCmd.ExecuteScalar();
                                    newExceptionName = result?.ToString() ?? "";
                                }
                            }

                            var newValue = string.IsNullOrEmpty(newExceptionName) && !clockInOverride.HasValue && !clockOutOverride.HasValue ?
                                "تم ازالة الاستثناء" :
                                (string.IsNullOrEmpty(newExceptionName) ? "" : $"استثناء: {newExceptionName}") +
                                (!clockInOverride.HasValue ? "" : $", دخول: {clockInOverride.Value:hh\\:mm\\:ss}") +
                                (!clockOutOverride.HasValue ? "" : $", خروج: {clockOutOverride.Value:hh\\:mm\\:ss}") +
                                (string.IsNullOrEmpty(entry.Notes) ? "" : $", ملاحظات: {entry.Notes}");

                            changesForAudit.Add((entry.UserId, entry.EmployeeName, entry.Date, oldValue, newValue));
                        }
                    }
                });

                // Log all changes to audit system (on UI thread)
                foreach (var change in changesForAudit)
                {
                    AuditLogger.Log(
                        "UPDATE",
                        "employee_exceptions",
                        change.UserId,
                        change.OldValue,
                        change.NewValue,
                        $"تعديل استثناء للموظف: {change.EmployeeName} بتاريخ: {change.Date:yyyy-MM-dd}"
                    );
                }

                MessageBox.Show($"{modifiedEntries.Count} record(s) have been saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                GenerateReport_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save changes: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Hide();
                this.Cursor = Cursors.Arrow;
            }
        }


        private void ExportToPdf_Click(object sender, RoutedEventArgs e)
        {
            if (ReportDataGrid.Items.Count == 0)
            {
                MessageBox.Show("There is no data to export.", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF file (*.pdf)|*.pdf",
                FileName = $"AttendanceReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        var document = new Itext.Document(Itext.PageSize.A4.Rotate(), 20, 20, 30, 30);
                        var writer = Itext.pdf.PdfWriter.GetInstance(document, fs);
                        document.Open();

                        string arialUnicodePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                        if (!File.Exists(arialUnicodePath)) arialUnicodePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "tahoma.ttf");

                        var baseFont = Itext.pdf.BaseFont.CreateFont(arialUnicodePath, Itext.pdf.BaseFont.IDENTITY_H, Itext.pdf.BaseFont.EMBEDDED);
                        var titleFont = new Itext.Font(baseFont, 16, Itext.Font.BOLD);
                        var headerFont = new Itext.Font(baseFont, 9, Itext.Font.BOLD);
                        var bodyFont = new Itext.Font(baseFont, 8, Itext.Font.NORMAL);

                        var title = new Itext.Paragraph("Attendance Report - تقرير الحضور", titleFont) { Alignment = Itext.Element.ALIGN_CENTER, SpacingAfter = 20f };
                        document.Add(title);

                        if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
                        {
                            var dateRange = new Itext.Paragraph($"From {StartDatePicker.SelectedDate.Value:yyyy-MM-dd} To {EndDatePicker.SelectedDate.Value:yyyy-MM-dd}", bodyFont) { Alignment = Itext.Element.ALIGN_CENTER, SpacingAfter = 15f };
                            document.Add(dateRange);
                        }

                        var table = new Itext.pdf.PdfPTable(8) { WidthPercentage = 100, RunDirection = Itext.pdf.PdfWriter.RUN_DIRECTION_RTL };
                        table.SetWidths(new float[] { 10f, 12f, 10f, 8f, 7f, 7f, 9f, 10f });

                        string[] headers = { "رقم البصمة", "اسم الموظف", "القسم", "التاريخ", "الحضور الفعلي", "الانصراف الفعلي", "الأوقات", "الاستثناءات" };

                        foreach (var header in headers)
                        {
                            var cell = new Itext.pdf.PdfPCell(new Itext.Phrase(header, headerFont))
                            {
                                BackgroundColor = new Itext.BaseColor(236, 240, 241),
                                HorizontalAlignment = Itext.Element.ALIGN_CENTER,
                                VerticalAlignment = Itext.Element.ALIGN_MIDDLE,
                                Padding = 8,
                                RunDirection = Itext.pdf.PdfWriter.RUN_DIRECTION_RTL
                            };
                            table.AddCell(cell);
                        }

                        foreach (DailyReportEntry row in ReportDataGrid.Items)
                        {
                            string[] rowData = {
                                row.BadgeNumber ?? "", row.EmployeeName ?? "", row.Department ?? "",
                                row.Date.ToString("yyyy-MM-dd"),
                                row.ActualClockInString ?? "", row.ActualClockOutString ?? "", row.AllClocks ?? "",
                                allExceptionTypes.FirstOrDefault(et => et.ExceptionTypeId == row.SelectedExceptionTypeId)?.ExceptionName ?? row.Exceptions ?? ""
                            };

                            for (int i = 0; i < rowData.Length; i++)
                            {
                                var cell = new Itext.pdf.PdfPCell(new Itext.Phrase(rowData[i], bodyFont))
                                {
                                    HorizontalAlignment = Itext.Element.ALIGN_CENTER,
                                    VerticalAlignment = Itext.Element.ALIGN_MIDDLE,
                                    Padding = 5
                                };
                                if (i == 1 || i == 2 || i == 7)
                                {
                                    cell.RunDirection = Itext.pdf.PdfWriter.RUN_DIRECTION_RTL;
                                }
                                if (!string.IsNullOrEmpty(row.HighlightColor) && row.HighlightColor != "Transparent")
                                {
                                    try
                                    {
                                        var color = System.Drawing.ColorTranslator.FromHtml(row.HighlightColor);
                                        cell.BackgroundColor = new Itext.BaseColor(color.R, color.G, color.B);
                                    }
                                    catch { }
                                }
                                table.AddCell(cell);
                            }
                        }

                        document.Add(table);
                        var footer = new Itext.Paragraph($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}", new Itext.Font(baseFont, 8, Itext.Font.ITALIC)) { Alignment = Itext.Element.ALIGN_RIGHT, SpacingBefore = 20f };
                        document.Add(footer);
                        document.Close();
                    }

                    MessageBox.Show("Report exported to PDF successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Audit log the PDF export
                    int recordCount = ReportDataGrid.Items.Count;
                    string fileName = Path.GetFileName(saveFileDialog.FileName);
                    AuditLogger.Log(
                        "REPORT",
                        "daily_reports",
                        null,
                        null,
                        $"Export Type: PDF, File: {fileName}, Records: {recordCount}",
                        $"Exported {recordCount} records to PDF: {fileName}"
                    );

                    var psi = new ProcessStartInfo { FileName = saveFileDialog.FileName, UseShellExecute = true };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export data: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AssignException_Click(object sender, RoutedEventArgs e)
        {
            var exceptionTypes = allExceptionTypes;
            var departments = allDepartments;
            var users = allEmployees.Where(u => u.UserId != -1).ToList();

            var bulkAssignWindow = new BulkAssignExceptionWindow(exceptionTypes, departments, users) { Owner = this };

            if (bulkAssignWindow.ShowDialog() == true)
            {
                var targetExceptionTypeId = bulkAssignWindow.SelectedExceptionType.ExceptionTypeId;
                var targetExceptionName = bulkAssignWindow.SelectedExceptionType.ExceptionName;
                var startDate = bulkAssignWindow.SelectedStartDate;
                var endDate = bulkAssignWindow.SelectedEndDate;
                var userIdsToUpdate = bulkAssignWindow.SelectedUserIds;
                var notes = bulkAssignWindow.Notes;

                if (MessageBox.Show($"Are you sure you want to apply this exception to {userIdsToUpdate.Count} employee(s) from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}?", "Confirm Assignment", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }

                LoadingOverlay.Show("جاري تطبيق الاستثناء...", "الرجاء الانتظار");
                this.Cursor = Cursors.Wait;
                try
                {
                    // Get employee names for audit log
                    var employeeNames = users.Where(u => userIdsToUpdate.Contains(u.UserId))
                        .Select(u => u.Name).ToList();

                    await Task.Run(() =>
                    {
                        using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                        {
                            conn.Open();
                            for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
                            {
                                var deleteSql = "DELETE FROM employee_exceptions WHERE user_id_fk = ANY(@userIds) AND exception_date = @date;";
                                using (var cmd = new NpgsqlCommand(deleteSql, conn))
                                {
                                    cmd.Parameters.AddWithValue("userIds", userIdsToUpdate);
                                    cmd.Parameters.AddWithValue("date", day);
                                    cmd.ExecuteNonQuery();
                                }

                                if (targetExceptionTypeId > 0 || !string.IsNullOrWhiteSpace(notes))
                                {
                                    var insertSql = @"INSERT INTO employee_exceptions (user_id_fk, exception_type_id_fk, exception_date, notes)
                                                      SELECT user_id, @exceptionId, @date, @notes FROM users WHERE user_id = ANY(@userIds);";
                                    using (var cmd = new NpgsqlCommand(insertSql, conn))
                                    {
                                        cmd.Parameters.AddWithValue("userIds", userIdsToUpdate);
                                        cmd.Parameters.AddWithValue("date", day);
                                        cmd.Parameters.AddWithValue("exceptionId", targetExceptionTypeId);
                                        cmd.Parameters.AddWithValue("notes", (object)notes ?? DBNull.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    });

                    // Log bulk assignment to audit system
                    var employeeNamesStr = string.Join(", ", employeeNames.Take(5));
                    if (employeeNames.Count > 5)
                        employeeNamesStr += $" و {employeeNames.Count - 5} آخرين";

                    AuditLogger.Log(
                        "INSERT",
                        "employee_exceptions",
                        null,
                        null,
                        $"استثناء: {targetExceptionName}" + (string.IsNullOrEmpty(notes) ? "" : $", ملاحظات: {notes}"),
                        $"تعيين استثناء جماعي لـ {userIdsToUpdate.Count} موظف ({employeeNamesStr}) من {startDate:yyyy-MM-dd} الى {endDate:yyyy-MM-dd}"
                    );

                    MessageBox.Show($"{userIdsToUpdate.Count} employee(s) have been updated for the selected date range.", "Assignment Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    GenerateReport_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during the update: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    LoadingOverlay.Hide();
                    this.Cursor = Cursors.Arrow;
                }
            }
        }


        // ✅ Add this method to ReportsWindow.xaml.cs
        private void AutoCleanupAbsenceExceptions()
        {
            try
            {
                using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
                {
                    conn.Open();

                    // ✅ CORRECTED: Match by badge_number and extract date from log_time
                    var cleanupSql = @"
                DELETE FROM attendance_exceptions ae
                WHERE ae.exception_type_id_fk IN (
                    SELECT exception_type_id 
                    FROM exception_types 
                    WHERE LOWER(exception_name) LIKE '%غياب%' 
                    OR LOWER(exception_name) LIKE '%absence%'
                )
                AND EXISTS (
                    SELECT 1 
                    FROM attendance_logs al
                    JOIN users u ON u.badge_number = al.user_badge_number
                    WHERE u.user_id = ae.user_id_fk
                    AND DATE(al.log_time) = ae.exception_date
                )";

                    using (var cmd = new NpgsqlCommand(cleanupSql, conn))
                    {
                        int deleted = cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"Auto-cleaned {deleted} absence exceptions");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto cleanup error: {ex.Message}");
            }
        }


        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            if (ReportDataGrid.Items.Count == 0)
            {
                MessageBox.Show("There is no data to print.", "Print Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tempPdfPath = Path.Combine(Path.GetTempPath(), $"print_{Guid.NewGuid()}.pdf");
            try
            {
                using (var fs = new FileStream(tempPdfPath, FileMode.Create))
                {
                    var document = new Itext.Document(Itext.PageSize.A4.Rotate(), 20, 20, 30, 30);
                    var writer = Itext.pdf.PdfWriter.GetInstance(document, fs);
                    document.Open();

                    string arialUnicodePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                    if (!File.Exists(arialUnicodePath)) arialUnicodePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "tahoma.ttf");

                    var baseFont = Itext.pdf.BaseFont.CreateFont(arialUnicodePath, Itext.pdf.BaseFont.IDENTITY_H, Itext.pdf.BaseFont.EMBEDDED);
                    var titleFont = new Itext.Font(baseFont, 16, Itext.Font.BOLD);
                    var headerFont = new Itext.Font(baseFont, 9, Itext.Font.BOLD);
                    var bodyFont = new Itext.Font(baseFont, 8, Itext.Font.NORMAL);

                    var title = new Itext.Paragraph("Attendance Report - تقرير الحضور", titleFont) { Alignment = Itext.Element.ALIGN_CENTER, SpacingAfter = 20f };
                    document.Add(title);

                    if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
                    {
                        var dateRange = new Itext.Paragraph($"From {StartDatePicker.SelectedDate.Value:yyyy-MM-dd} To {EndDatePicker.SelectedDate.Value:yyyy-MM-dd}", bodyFont) { Alignment = Itext.Element.ALIGN_CENTER, SpacingAfter = 15f };
                        document.Add(dateRange);
                    }

                    var table = new Itext.pdf.PdfPTable(8) { WidthPercentage = 100, RunDirection = Itext.pdf.PdfWriter.RUN_DIRECTION_RTL };
                    table.SetWidths(new float[] { 10f, 12f, 10f, 8f, 7f, 7f, 9f, 10f });

                    string[] headers = { "رقم البصمة", "اسم الموظف", "القسم", "التاريخ", "الحضور الفعلي", "الانصراف الفعلي", "الأوقات", "الاستثناءات" };

                    foreach (var header in headers)
                    {
                        var cell = new Itext.pdf.PdfPCell(new Itext.Phrase(header, headerFont))
                        {
                            BackgroundColor = new Itext.BaseColor(236, 240, 241),
                            HorizontalAlignment = Itext.Element.ALIGN_CENTER,
                            VerticalAlignment = Itext.Element.ALIGN_MIDDLE,
                            Padding = 8,
                            RunDirection = Itext.pdf.PdfWriter.RUN_DIRECTION_RTL
                        };
                        table.AddCell(cell);
                    }

                    foreach (DailyReportEntry row in ReportDataGrid.Items)
                    {
                        string[] rowData = {
                            row.BadgeNumber ?? "", row.EmployeeName ?? "", row.Department ?? "",
                            row.Date.ToString("yyyy-MM-dd"),
                            row.ActualClockInString ?? "", row.ActualClockOutString ?? "", row.AllClocks ?? "",
                            allExceptionTypes.FirstOrDefault(et => et.ExceptionTypeId == row.SelectedExceptionTypeId)?.ExceptionName ?? row.Exceptions ?? ""
                        };

                        for (int i = 0; i < rowData.Length; i++)
                        {
                            var cell = new Itext.pdf.PdfPCell(new Itext.Phrase(rowData[i], bodyFont))
                            {
                                HorizontalAlignment = Itext.Element.ALIGN_CENTER,
                                VerticalAlignment = Itext.Element.ALIGN_MIDDLE,
                                Padding = 5
                            };
                            if (i == 1 || i == 2 || i == 7) { cell.RunDirection = Itext.pdf.PdfWriter.RUN_DIRECTION_RTL; }
                            if (!string.IsNullOrEmpty(row.HighlightColor) && row.HighlightColor != "Transparent")
                            {
                                try
                                {
                                    var color = System.Drawing.ColorTranslator.FromHtml(row.HighlightColor);
                                    cell.BackgroundColor = new Itext.BaseColor(color.R, color.G, color.B);
                                }
                                catch { }
                            }
                            table.AddCell(cell);
                        }
                    }
                    document.Add(table);
                    document.Close();
                }

                Process.Start(tempPdfPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the report for printing. \n\nError: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
        #endregion

        #region Report Logic
        private List<EmployeeException> FetchEmployeeExceptions(DateTime startDate, DateTime endDate, int departmentId, int employeeId)
        {
            var exceptions = new List<EmployeeException>();
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sqlBuilder = new StringBuilder(@"
                    SELECT ee.user_id_fk, ee.exception_type_id_fk, ee.exception_date, et.exception_name, ee.notes, ee.clock_in_override, ee.clock_out_override
                    FROM employee_exceptions ee
                    LEFT JOIN exception_types et ON ee.exception_type_id_fk = et.exception_type_id
                    INNER JOIN users u ON ee.user_id_fk = u.user_id
                    WHERE ee.exception_date >= @startDate AND ee.exception_date <= @endDate ");

                var cmd = new NpgsqlCommand();
                cmd.Parameters.AddWithValue("startDate", startDate);
                cmd.Parameters.AddWithValue("endDate", endDate);

                if (CurrentUser.Role == "deptadmin")
                {
                    sqlBuilder.Append("AND u.default_dept_id = ANY(@permittedIds) ");
                    cmd.Parameters.AddWithValue("permittedIds", CurrentUser.PermittedDepartmentIds);
                }

                if (departmentId > 0) { sqlBuilder.Append("AND u.default_dept_id = @deptId "); cmd.Parameters.AddWithValue("deptId", departmentId); }
                if (employeeId > 0) { sqlBuilder.Append("AND u.user_id = @userId "); cmd.Parameters.AddWithValue("userId", employeeId); }

                cmd.CommandText = sqlBuilder.ToString();
                cmd.Connection = conn;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        exceptions.Add(new EmployeeException
                        {
                            UserId = reader.GetInt32(0),
                            ExceptionTypeId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            ExceptionDate = reader.GetDateTime(2),
                            ExceptionName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Notes = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            ClockInOverride = reader.IsDBNull(5) ? (TimeSpan?)null : reader.GetTimeSpan(5),
                            ClockOutOverride = reader.IsDBNull(6) ? (TimeSpan?)null : reader.GetTimeSpan(6)
                        });
                    }
                }
            }
            return exceptions;
        }

        private List<AttendanceLog> FetchRawLogs(DateTime startDate, DateTime endDate, int departmentId, int employeeId)
        {
            var rawLogs = new List<AttendanceLog>();
            var endDateInclusive = endDate.AddDays(1);

            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();
                var sqlBuilder = new StringBuilder(@"
                    SELECT al.log_time, al.user_badge_number, u.name, d.dept_name, u.user_id, s.start_time, s.end_time
                    FROM attendance_logs al
                    INNER JOIN users u ON al.user_badge_number = u.badge_number
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    WHERE al.log_time >= @startDate AND al.log_time < @endDate ");

                var cmd = new NpgsqlCommand();
                cmd.Parameters.AddWithValue("startDate", startDate);
                cmd.Parameters.AddWithValue("endDate", endDateInclusive);

                if (CurrentUser.Role == "deptadmin")
                {
                    sqlBuilder.Append("AND u.default_dept_id = ANY(@permittedIds) ");
                    cmd.Parameters.AddWithValue("permittedIds", CurrentUser.PermittedDepartmentIds);
                }

                if (departmentId > 0) { sqlBuilder.Append("AND u.default_dept_id = @deptId "); cmd.Parameters.AddWithValue("deptId", departmentId); }
                if (employeeId > 0) { sqlBuilder.Append("AND u.user_id = @userId "); cmd.Parameters.AddWithValue("userId", employeeId); }

                sqlBuilder.Append("ORDER BY al.log_time ASC");
                cmd.CommandText = sqlBuilder.ToString();
                cmd.Connection = conn;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var log = new AttendanceLog
                        {
                            LogTime = reader.GetDateTime(0),
                            UserBadgeNumber = reader.GetString(1),
                            Name = reader.GetString(2),
                            Departments = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                            UserId = reader.GetInt32(4)
                        };

                        if (!reader.IsDBNull(5)) log.RequiredClockIn = reader.GetTimeSpan(5);
                        if (!reader.IsDBNull(6)) log.RequiredClockOut = reader.GetTimeSpan(6);

                        rawLogs.Add(log);
                    }
                }
            }
            return rawLogs.ToList();
        }

        private List<DailyReportEntry> ProcessLogsIntoDailySummary(List<AttendanceLog> rawLogs, List<EmployeeException> exceptions, DateTime startDate, DateTime endDate, bool autoCalculate, int departmentId, int employeeId)
        {
            var dailyReports = new List<DailyReportEntry>();
            var usersInFilter = allEmployees.Where(u => u.UserId != -1).ToList();

            if (employeeId > 0)
                usersInFilter = usersInFilter.Where(u => u.UserId == employeeId).ToList();
            else if (departmentId > 0)
                usersInFilter = usersInFilter.Where(u => u.DefaultDeptId == departmentId).ToList();

            var shiftInfoCache = new Dictionary<int, (TimeSpan?, TimeSpan?)>();
            foreach (var log in rawLogs.Where(l => l.RequiredClockIn.HasValue && l.RequiredClockOut.HasValue))
            {
                if (!shiftInfoCache.ContainsKey(log.UserId))
                    shiftInfoCache[log.UserId] = (log.RequiredClockIn, log.RequiredClockOut);
            }

            foreach (var user in usersInFilter)
            {
                for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
                {
                    var logsForThisDay = rawLogs
                        .Where(l => l.UserId == user.UserId && l.LogTime.Date == day)
                        .OrderBy(l => l.LogTime)
                        .ToList();

                    // Remove duplicates - keep LAST timestamp of each 30-second window
                    var uniqueLogs = new List<AttendanceLog>();
                    for (int i = 0; i < logsForThisDay.Count; i++)
                    {
                        if (i + 1 < logsForThisDay.Count &&
                            (logsForThisDay[i + 1].LogTime - logsForThisDay[i].LogTime).TotalSeconds <= 30)
                        {
                            continue;
                        }
                        uniqueLogs.Add(logsForThisDay[i]);
                    }

                    logsForThisDay = uniqueLogs;

                    var manualException = exceptions.FirstOrDefault(ex => ex.UserId == user.UserId && ex.ExceptionDate.Date == day);

                    var entry = new DailyReportEntry
                    {
                        UserId = user.UserId,
                        EmployeeId = user.UserId.ToString(),
                        BadgeNumber = user.BadgeNumber,
                        EmployeeName = user.Name,
                        Department = allDepartments.FirstOrDefault(d => d.DeptId == user.DefaultDeptId)?.DeptName ?? "Unknown",
                        Date = day,
                        RequiredClockIn = shiftInfoCache.ContainsKey(user.UserId) ? shiftInfoCache[user.UserId].Item1 : null,
                        RequiredClockOut = shiftInfoCache.ContainsKey(user.UserId) ? shiftInfoCache[user.UserId].Item2 : null,
                        AvailableExceptions = allExceptionTypes,
                        Notes = manualException?.Notes ?? ""
                    };

                    DateTime? actualClockIn = null;
                    DateTime? actualClockOut = null;

                    if (logsForThisDay.Any())
                    {
                        actualClockIn = logsForThisDay.First().LogTime;

                        if (logsForThisDay.Count > 1)
                        {
                            actualClockOut = logsForThisDay.Last().LogTime;
                        }

                        entry.AllClocks = string.Join(", ", logsForThisDay.Select(l => l.LogTime.ToString("HH:mm:ss")));
                    }

                    // Apply manual overrides if they exist
                    if (manualException?.ClockInOverride.HasValue == true)
                        actualClockIn = day.Date + manualException.ClockInOverride.Value;

                    if (manualException?.ClockOutOverride.HasValue == true)
                        actualClockOut = day.Date + manualException.ClockOutOverride.Value;

                    // Update AllClocks to show manual override times
                    if (manualException != null && (manualException.ClockInOverride.HasValue || manualException.ClockOutOverride.HasValue))
                    {
                        var manualTimes = new List<string>();
                        if (actualClockIn.HasValue) manualTimes.Add(actualClockIn.Value.ToString("HH:mm:ss"));
                        if (actualClockOut.HasValue) manualTimes.Add(actualClockOut.Value.ToString("HH:mm:ss"));
                        entry.AllClocks = string.Join(", ", manualTimes);
                    }

                    entry.ActualClockInString = actualClockIn?.ToString("HH:mm:ss");
                    entry.ActualClockOutString = actualClockOut?.ToString("HH:mm:ss");

                    // ✅ EXCEPTION HANDLING
                    if (manualException != null)
                    {
                        // Check if saved "missing punch" exception is still valid
                        bool hasBothPunches = actualClockIn.HasValue && actualClockOut.HasValue;
                        bool isMissingPunchException = manualException.ExceptionName != null &&
                            (manualException.ExceptionName.Contains("عدم وجود بصمة") ||
                             manualException.ExceptionName.ToLower().Contains("missing"));

                        // Also check for absence exception when employee has attendance
                        bool hasAnyPunch = actualClockIn.HasValue || actualClockOut.HasValue;
                        bool isAbsenceException = manualException.ExceptionName != null &&
                            (manualException.ExceptionName.Contains("غياب") ||
                             manualException.ExceptionName.ToLower().Contains("absence"));

                        if (hasBothPunches && isMissingPunchException)
                        {
                            // Employee now has both punches - ignore outdated "missing punch" exception
                            // Let auto-calculate handle it (check for late/early instead)
                            entry.SelectedExceptionTypeId = 0;
                        }
                        else if (hasAnyPunch && isAbsenceException)
                        {
                            // Employee has attendance but saved as "absent" - ignore outdated exception
                            entry.SelectedExceptionTypeId = 0;
                        }
                        else
                        {
                            // Valid manual exception - use it
                            entry.SelectedExceptionTypeId = manualException.ExceptionTypeId;
                        }
                    }

                    // Auto-calculate if no valid exception set
                    if (entry.SelectedExceptionTypeId == 0 && autoCalculate)
                    {
                        // ✅ Auto-calculate exceptions
                        bool hasShift = entry.RequiredClockIn.HasValue || entry.RequiredClockOut.HasValue;

                        // Check if employee is absent (no logs and has a shift)
                        // Check if employee is absent (no logs and has a shift)
                        if (!logsForThisDay.Any())
                        {
                            var absentException = allExceptionTypes.FirstOrDefault(et =>
                                et.ExceptionName.Contains("غياب") || et.ExceptionName.ToLower().Contains("absence"));

                            if (absentException != null)
                            {
                                entry.SelectedExceptionTypeId = absentException.ExceptionTypeId;
                            }
                        }

                        // Has logs - check for other issues
                        else if (logsForThisDay.Any() && hasShift)
                        {
                            bool hasClockIn = actualClockIn.HasValue;
                            bool hasClockOut = actualClockOut.HasValue;

                            // Missing punch detection
                            if (hasClockIn && !hasClockOut)
                            {
                                // Missing clock-out - look for specific exception
                                var missingPunchException = allExceptionTypes.FirstOrDefault(et =>
                                    et.ExceptionName == "عدم وجود بصمة خروج") ??
                                    allExceptionTypes.FirstOrDefault(et =>
                                    et.ExceptionName.Contains("عدم وجود بصمة") || et.ExceptionName.ToLower().Contains("missing"));

                                if (missingPunchException != null)
                                {
                                    entry.SelectedExceptionTypeId = missingPunchException.ExceptionTypeId;
                                }
                            }
                            else if (!hasClockIn && hasClockOut)
                            {
                                // Missing clock-in - look for specific exception
                                var missingPunchException = allExceptionTypes.FirstOrDefault(et =>
                                    et.ExceptionName == "عدم وجود بصمة دخول") ??
                                    allExceptionTypes.FirstOrDefault(et =>
                                    et.ExceptionName.Contains("عدم وجود بصمة") || et.ExceptionName.ToLower().Contains("missing"));

                                if (missingPunchException != null)
                                {
                                    entry.SelectedExceptionTypeId = missingPunchException.ExceptionTypeId;
                                }
                            }
                            // Both punches exist - check for late/early
                            else if (hasClockIn && hasClockOut)
                            {
                                bool isLate = false;
                                bool isEarly = false;

                                // Check late arrival (more than 1 minute late)
                                if (entry.RequiredClockIn.HasValue)
                                {
                                    var actualInTime = actualClockIn.Value.TimeOfDay;
                                    if (actualInTime > entry.RequiredClockIn.Value.Add(TimeSpan.FromMinutes(1)))
                                    {
                                        isLate = true;
                                    }
                                }

                                // Check early departure (more than 1 minute early)
                                if (entry.RequiredClockOut.HasValue)
                                {
                                    var actualOutTime = actualClockOut.Value.TimeOfDay;
                                    if (actualOutTime < entry.RequiredClockOut.Value.Subtract(TimeSpan.FromMinutes(1)))
                                    {
                                        isEarly = true;
                                    }
                                }

                                // ✅ FIX: Set specific exception based on WHAT actually happened
                                // Priority: late arrival > early departure (if both, late is more important)
                                if (isLate)
                                {
                                    // Look for late arrival exception specifically
                                    var lateException = allExceptionTypes.FirstOrDefault(et =>
                                        et.ExceptionName == "تأخير وقت الدخول" ||
                                        et.ExceptionName == "تأخير بصمة الدخول");

                                    // Fallback to generic late exception
                                    if (lateException == null)
                                    {
                                        lateException = allExceptionTypes.FirstOrDefault(et =>
                                            et.ExceptionName.Contains("تأخير") && !et.ExceptionName.Contains("خروج"));
                                    }

                                    if (lateException != null)
                                    {
                                        entry.SelectedExceptionTypeId = lateException.ExceptionTypeId;
                                    }
                                }
                                else if (isEarly)
                                {
                                    // Look for early departure exception specifically
                                    var earlyException = allExceptionTypes.FirstOrDefault(et =>
                                        et.ExceptionName == "بصمة قبل وقت الخروج" ||
                                        et.ExceptionName.Contains("قبل وقت الخروج"));

                                    // Fallback to generic early exception
                                    if (earlyException == null)
                                    {
                                        earlyException = allExceptionTypes.FirstOrDefault(et =>
                                            et.ExceptionName.ToLower().Contains("early") ||
                                            (et.ExceptionName.Contains("قبل") && et.ExceptionName.Contains("خروج")));
                                    }

                                    if (earlyException != null)
                                    {
                                        entry.SelectedExceptionTypeId = earlyException.ExceptionTypeId;
                                    }
                                }
                            }
                        }
                    }

                    // ✅ Set the highlight color based on the exception
                    SetHighlightColor(entry);

                    dailyReports.Add(entry);
                }
            }

            return dailyReports.OrderBy(r => r.EmployeeName).ThenBy(r => r.Date).ToList();
        }




        private void CalculateAutomaticExceptions(DailyReportEntry entry, DateTime? actualClockIn, DateTime? actualClockOut)
        {
            int exceptionIdToSet = 0;

            // ✅ NEW: Check for missing clock-in
            if (!actualClockIn.HasValue && actualClockOut.HasValue)
            {
                var missingPunchException = allExceptionTypes.FirstOrDefault(et => et.ExceptionName == "عدم وجود بصمة دخول");
                if (missingPunchException != null)
                    exceptionIdToSet = missingPunchException.ExceptionTypeId;
            }
            // Check for missing clock-out
            else if (actualClockIn.HasValue && !actualClockOut.HasValue)
            {
                var missingPunchException = allExceptionTypes.FirstOrDefault(et => et.ExceptionName == "عدم وجود بصمة خروج");
                if (missingPunchException != null)
                    exceptionIdToSet = missingPunchException.ExceptionTypeId;
            }
            // Check for late arrival
            else if (entry.RequiredClockIn.HasValue && actualClockIn.HasValue &&
                actualClockIn.Value.TimeOfDay > entry.RequiredClockIn.Value.Add(new TimeSpan(0, 1, 0)))
            {
                var lateInException = allExceptionTypes.FirstOrDefault(et => et.ExceptionName == "تأخير وقت الدخول");
                if (lateInException != null)
                    exceptionIdToSet = lateInException.ExceptionTypeId;
            }
            // Check for early departure
            else if (entry.RequiredClockOut.HasValue && actualClockOut.HasValue &&
                actualClockOut.Value.TimeOfDay < entry.RequiredClockOut.Value)
            {
                var earlyOutException = allExceptionTypes.FirstOrDefault(et => et.ExceptionName == "بصمة قبل وقت الخروج");
                if (earlyOutException != null)
                    exceptionIdToSet = earlyOutException.ExceptionTypeId;
            }

            if (exceptionIdToSet > 0)
            {
                entry.SelectedExceptionTypeId = exceptionIdToSet;
            }
        }

        private void SetHighlightColor(DailyReportEntry entry)
        {
            string colorToSet = "Transparent"; // Default

            bool hasClockIn = !string.IsNullOrWhiteSpace(entry.ActualClockInString) && entry.ActualClockInString != "N/A";
            bool hasClockOut = !string.IsNullOrWhiteSpace(entry.ActualClockOutString) && entry.ActualClockOutString != "N/A";

            // Priority 1: Explicit exceptions (highest priority)
            if (entry.SelectedExceptionTypeId > 0)
            {
                var exceptionName = allExceptionTypes.FirstOrDefault(et => et.ExceptionTypeId == entry.SelectedExceptionTypeId)?.ExceptionName ?? "";

                if (exceptionName.Contains("غياب") || exceptionName.ToLower().Contains("absence"))
                {
                    colorToSet = "#FFD6D6"; // Dark Red for absence
                }
                else if (exceptionName.Contains("عدم وجود بصمة") || exceptionName.ToLower().Contains("missing punch"))
                {
                    colorToSet = "#FFE6E6"; // Light Red/Pink for missing punch
                }
                else if (exceptionName.Contains("تأخير") || exceptionName.Contains("قبل وقت") ||
                         exceptionName.ToLower().Contains("late") || exceptionName.ToLower().Contains("early"))
                {
                    colorToSet = "#FFF9E5"; // Yellow for late/early
                }
                else if (exceptionName.Contains("اجازة") || exceptionName.ToLower().Contains("leave"))
                {
                    colorToSet = "#EBF5FB"; // Blue for leave
                }
                else if (exceptionName.Contains("واجب") || exceptionName.Contains("تكليف") ||
                         exceptionName.Contains("ايفاد") || exceptionName.Contains("دورة") ||
                         exceptionName.Contains("ندوة") || exceptionName.Contains("ورشة") ||
                         exceptionName.ToLower().Contains("duty") || exceptionName.ToLower().Contains("training"))
                {
                    colorToSet = "#E8F8F5"; // Green for official duty
                }
                else
                {
                    colorToSet = "#F0F0F0"; // Light gray for other exceptions
                }
            }
            // Priority 2: Attendance issues (only if no explicit exception)
            else
            {
                // ✅ Check if user has a shift assigned (should be working this day)
                bool hasShift = entry.RequiredClockIn.HasValue || entry.RequiredClockOut.HasValue;

                // Missing both punches = Absence (only if they have a shift)
                if (!hasClockIn && !hasClockOut && hasShift)
                {
                    colorToSet = "#FFD6D6"; // Dark Red for absence

                    // ✅ Auto-assign absence exception if not already set
                    var absentException = allExceptionTypes.FirstOrDefault(et =>
                        et.ExceptionName.Contains("غياب") || et.ExceptionName.ToLower().Contains("absence"));
                    if (absentException != null && entry.SelectedExceptionTypeId == 0)
                    {
                        entry.SelectedExceptionTypeId = absentException.ExceptionTypeId;
                    }
                }
                // Missing one punch = Missing punch
                else if (hasShift && ((hasClockIn && !hasClockOut) || (!hasClockIn && hasClockOut)))
                {
                    colorToSet = "#FFE6E6"; // Light Red/Pink for missing punch

                    // ✅ Auto-assign missing punch exception if not already set
                    // Use specific exception based on which punch is missing
                    if (entry.SelectedExceptionTypeId == 0)
                    {
                        ExceptionType missingPunchException = null;

                        if (hasClockIn && !hasClockOut)
                        {
                            // Missing clock-out
                            missingPunchException = allExceptionTypes.FirstOrDefault(et =>
                                et.ExceptionName == "عدم وجود بصمة خروج") ??
                                allExceptionTypes.FirstOrDefault(et =>
                                et.ExceptionName.Contains("عدم وجود بصمة") || et.ExceptionName.ToLower().Contains("missing"));
                        }
                        else if (!hasClockIn && hasClockOut)
                        {
                            // Missing clock-in
                            missingPunchException = allExceptionTypes.FirstOrDefault(et =>
                                et.ExceptionName == "عدم وجود بصمة دخول") ??
                                allExceptionTypes.FirstOrDefault(et =>
                                et.ExceptionName.Contains("عدم وجود بصمة") || et.ExceptionName.ToLower().Contains("missing"));
                        }

                        if (missingPunchException != null)
                        {
                            entry.SelectedExceptionTypeId = missingPunchException.ExceptionTypeId;
                        }
                    }
                }
                // Has both punches - check for late arrival or early departure
                else if (hasClockIn && hasClockOut && hasShift)
                {
                    bool isLate = false;
                    bool isEarly = false;

                    // Check for late arrival (more than 1 minute late)
                    if (entry.RequiredClockIn.HasValue && TimeSpan.TryParse(entry.ActualClockInString, out var actualIn))
                    {
                        if (actualIn > entry.RequiredClockIn.Value.Add(new TimeSpan(0, 1, 0)))
                        {
                            isLate = true;
                        }
                    }

                    // Check for early departure (any time before required clock out)
                    if (entry.RequiredClockOut.HasValue && TimeSpan.TryParse(entry.ActualClockOutString, out var actualOut))
                    {
                        if (actualOut < entry.RequiredClockOut.Value.Subtract(new TimeSpan(0, 1, 0)))
                        {
                            isEarly = true;
                        }
                    }

                    // Highlight if late or early
                    if (isLate || isEarly)
                    {
                        colorToSet = "#FFF9E5"; // Yellow for late/early

                        // ✅ Auto-assign late/early exception if not already set
                        var lateEarlyException = allExceptionTypes.FirstOrDefault(et =>
                            et.ExceptionName.Contains("تأخير") || et.ExceptionName.Contains("قبل وقت") ||
                            et.ExceptionName.ToLower().Contains("late") || et.ExceptionName.ToLower().Contains("early"));
                        if (lateEarlyException != null && entry.SelectedExceptionTypeId == 0)
                        {
                            entry.SelectedExceptionTypeId = lateEarlyException.ExceptionTypeId;
                        }
                    }
                }
            }

            entry.HighlightColor = colorToSet;

            // ✅ Debug logging (optional - can be removed in production)
            if (colorToSet != "Transparent")
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Highlight: {entry.EmployeeName} on {entry.Date:yyyy-MM-dd} | " +
                    $"Color: {colorToSet} | " +
                    $"ClockIn: {entry.ActualClockInString ?? "N/A"} | " +
                    $"ClockOut: {entry.ActualClockOutString ?? "N/A"} | " +
                    $"Exception: {(entry.SelectedExceptionTypeId > 0 ? allExceptionTypes.FirstOrDefault(et => et.ExceptionTypeId == entry.SelectedExceptionTypeId)?.ExceptionName : "None")}"
                );
            }
        }



    }
    #endregion
}
