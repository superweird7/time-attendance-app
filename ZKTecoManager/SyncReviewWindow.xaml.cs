using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ZKTecoManager.Models.Sync;

namespace ZKTecoManager
{
    /// <summary>
    /// Interaction logic for SyncReviewWindow.xaml
    /// </summary>
    public partial class SyncReviewWindow : Window
    {
        private readonly List<PendingChange> _changes;
        private readonly RemoteLocation _location;

        public List<PendingChange> ApprovedChanges { get; private set; }

        public SyncReviewWindow(List<PendingChange> changes, RemoteLocation location)
        {
            InitializeComponent();
            _changes = changes;
            _location = location;
            ApprovedChanges = new List<PendingChange>();

            InitializeData();
        }

        private void InitializeData()
        {
            LocationNameText.Text = $"الموقع: {_location.LocationName}";

            // Summary
            var newCount = _changes.Count(c => c.ChangeType == ChangeType.New);
            var updateCount = _changes.Count(c => c.ChangeType == ChangeType.Updated);
            var conflictCount = _changes.Count(c => c.ChangeType == ChangeType.Conflict);

            SummaryText.Text = $"إجمالي التغييرات: {_changes.Count} ({newCount} جديد، {updateCount} تحديث، {conflictCount} تعارض)";

            // Bind to grid
            ChangesGrid.ItemsSource = _changes;

            // Subscribe to property changes
            foreach (var change in _changes)
            {
                change.PropertyChanged += Change_PropertyChanged;
            }

            UpdateSelectedCount();
        }

        private void Change_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PendingChange.IsApproved))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            var count = _changes.Count(c => c.IsApproved);
            SelectedCountText.Text = $"تم تحديد {count} تغيير";
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
            DialogResult = false;
            Close();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var change in _changes)
            {
                change.IsApproved = true;
            }
            ChangesGrid.Items.Refresh();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var change in _changes)
            {
                change.IsApproved = false;
            }
            ChangesGrid.Items.Refresh();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApprovedChanges = _changes.Where(c => c.IsApproved).ToList();

            if (!ApprovedChanges.Any())
            {
                MessageBox.Show("الرجاء تحديد تغيير واحد على الأقل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var conflictCount = ApprovedChanges.Count(c => c.ChangeType == ChangeType.Conflict);
            if (conflictCount > 0)
            {
                var result = MessageBox.Show(
                    $"هناك {conflictCount} تعارض سيتم تطبيقه. هذا سيؤدي إلى استبدال البيانات المحلية.\n\nهل تريد المتابعة؟",
                    "تأكيد التعارضات",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
