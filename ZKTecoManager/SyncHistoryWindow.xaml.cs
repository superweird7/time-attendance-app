using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ZKTecoManager
{
    /// <summary>
    /// Sync history viewer window
    /// </summary>
    public partial class SyncHistoryWindow : Window
    {
        private readonly List<SyncHistoryEntry> _history;

        public SyncHistoryWindow(List<SyncHistoryEntry> history)
        {
            InitializeComponent();
            _history = history;
            LoadHistory();
        }

        private void LoadHistory()
        {
            // Calculate stats
            TotalSyncsText.Text = _history.Count.ToString();
            SuccessfulSyncsText.Text = _history.Count(h => h.Status == "نجاح" || h.Status == "Success").ToString();
            FailedSyncsText.Text = _history.Count(h => h.Status != "نجاح" && h.Status != "Success" && !string.IsNullOrEmpty(h.Status)).ToString();
            TotalRecordsText.Text = _history.Sum(h => h.RecordsAdded + h.RecordsUpdated).ToString("N0");

            // Bind to grid
            HistoryGrid.ItemsSource = _history;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Sync history entry model
    /// </summary>
    public class SyncHistoryEntry
    {
        public int SyncId { get; set; }
        public string LocationName { get; set; }
        public string SyncType { get; set; }
        public int RecordsAdded { get; set; }
        public int RecordsUpdated { get; set; }
        public int RecordsSkipped { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int DurationSeconds { get; set; }
        public bool IsIncremental { get; set; }

        // Display properties
        public string StatusDisplay => Status == "نجاح" || Status == "Success" ? "✓" : "✗";

        public Brush StatusColor
        {
            get
            {
                if (Status == "نجاح" || Status == "Success")
                    return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                else if (string.IsNullOrEmpty(Status))
                    return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray
                else
                    return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            }
        }

        public string DurationDisplay
        {
            get
            {
                if (DurationSeconds < 60)
                    return $"{DurationSeconds} ث";
                else
                    return $"{DurationSeconds / 60} د {DurationSeconds % 60} ث";
            }
        }

        public string SyncTypeDisplay => IsIncremental ? "تزايدي" : "كامل";
    }
}
