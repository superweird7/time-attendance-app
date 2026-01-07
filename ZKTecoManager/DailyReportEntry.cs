using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ZKTecoManager;

public class DailyReportEntry : INotifyPropertyChanged
{
    private string _highlightColor;
    private int _selectedExceptionTypeId;
    private string _actualClockInString;
    private string _actualClockOutString;
    private string _notes;

    public int UserId { get; set; }
    public string EmployeeId { get; set; }
    public string BadgeNumber { get; set; }
    public string EmployeeName { get; set; }
    public string Department { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? RequiredClockIn { get; set; }
    public TimeSpan? RequiredClockOut { get; set; }
    public string AllClocks { get; set; }
    public string Exceptions { get; set; }
    public List<ExceptionType> AvailableExceptions { get; set; }
    public bool IsModified { get; set; }

    public string HighlightColor
    {
        get => _highlightColor;
        set
        {
            if (_highlightColor != value)
            {
                _highlightColor = value;
                OnPropertyChanged(nameof(HighlightColor));
            }
        }
    }

    public int SelectedExceptionTypeId
    {
        get => _selectedExceptionTypeId;
        set
        {
            if (_selectedExceptionTypeId != value)
            {
                _selectedExceptionTypeId = value;
                IsModified = true;
                OnPropertyChanged(nameof(SelectedExceptionTypeId));
                OnPropertyChanged(nameof(SelectedExceptionName));
                UpdateHighlight(); // ✅ Added
            }
        }
    }

    // ✅ NEW: Property to get exception name
    public string SelectedExceptionName
    {
        get
        {
            var exception = AvailableExceptions?.FirstOrDefault(e => e.ExceptionTypeId == SelectedExceptionTypeId);
            return exception?.ExceptionName ?? "لا يوجد";
        }
    }

    public string ActualClockInString
    {
        get => _actualClockInString;
        set
        {
            if (_actualClockInString != value)
            {
                _actualClockInString = value;
                IsModified = true;
                OnPropertyChanged(nameof(ActualClockInString));
                UpdateHighlight(); // ✅ Added
            }
        }
    }

    public string ActualClockOutString
    {
        get => _actualClockOutString;
        set
        {
            if (_actualClockOutString != value)
            {
                _actualClockOutString = value;
                IsModified = true;
                OnPropertyChanged(nameof(ActualClockOutString));
                UpdateHighlight(); // ✅ Added
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                IsModified = true;
                OnPropertyChanged(nameof(Notes));
            }
        }
    }

    // ✅ NEW: Real-time highlight update method
    private void UpdateHighlight()
    {
        string newHighlight = null;

        // Get exception name
        string exceptionName = SelectedExceptionName?.ToLower() ?? "";

        // Priority 1: Exception-based highlighting
        if (exceptionName.Contains("غياب") || exceptionName.Contains("absence"))
        {
            newHighlight = "#FF4444"; // Red for absence
        }
        else if (exceptionName.Contains("تأخير") || exceptionName.Contains("late"))
        {
            newHighlight = "#FFF9E5"; // Yellow for late
        }
        else if (exceptionName.Contains("إجازة") || exceptionName.Contains("leave"))
        {
            newHighlight = "#EBF5FB"; // Blue for leave
        }
        else if (exceptionName.Contains("مأمورية") || exceptionName.Contains("duty"))
        {
            newHighlight = "#E8F8F5"; // Green for duty
        }
        else if (exceptionName == "لا يوجد" || string.IsNullOrEmpty(exceptionName))
        {
            // Priority 2: Check actual attendance
            bool hasClockIn = !string.IsNullOrWhiteSpace(ActualClockInString) &&
                              ActualClockInString != "N/A" &&
                              ActualClockInString != "";

            bool hasClockOut = !string.IsNullOrWhiteSpace(ActualClockOutString) &&
                               ActualClockOutString != "N/A" &&
                               ActualClockOutString != "";

            if (!hasClockIn && !hasClockOut)
            {
                newHighlight = "#FF4444"; // Red - Completely absent
            }
            else if (!hasClockIn || !hasClockOut)
            {
                newHighlight = "#FFF9E5"; // Yellow - Incomplete attendance
            }
            // If both times exist and no exception, no highlight (normal attendance)
        }

        HighlightColor = newHighlight;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
