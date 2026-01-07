using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager
{
    public class PunchTimeViewModel : INotifyPropertyChanged
    {
        private string _timeValue;
        public string TimeValue
        {
            get => _timeValue;
            set
            {
                _timeValue = value;
                OnPropertyChanged(nameof(TimeValue));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class AddEditShiftWindow : Window
    {
        public Shift ShiftData { get; private set; }
        private bool isEditMode;
        private ObservableCollection<PunchTimeViewModel> PunchTimes { get; set; }

        public AddEditShiftWindow(Shift shiftToEdit = null)
        {
            InitializeComponent();
            ShiftData = shiftToEdit ?? new Shift();
            isEditMode = (shiftToEdit != null);

            PunchTimes = new ObservableCollection<PunchTimeViewModel>();
            PunchTimesItemsControl.ItemsSource = PunchTimes;

            if (isEditMode)
            {
                TitleText.Text = "تعديل وردية";
                ShiftNameTextBox.Text = ShiftData.ShiftName;
                foreach (var rule in ShiftData.Rules.OrderBy(t => t))
                {
                    PunchTimes.Add(new PunchTimeViewModel { TimeValue = rule.ToString(@"hh\:mm") });
                }
            }
            else
            {
                PunchTimes.Add(new PunchTimeViewModel { TimeValue = "08:00" });
                PunchTimes.Add(new PunchTimeViewModel { TimeValue = "15:00" });
            }
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

        private void AddTime_Click(object sender, RoutedEventArgs e)
        {
            PunchTimes.Add(new PunchTimeViewModel { TimeValue = "00:00" });
        }

        private void RemoveTime_Click(object sender, RoutedEventArgs e)
        {
            var timeToRemove = (sender as FrameworkElement)?.Tag as PunchTimeViewModel;
            if (timeToRemove != null)
            {
                PunchTimes.Remove(timeToRemove);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ShiftNameTextBox.Text))
            {
                MessageBox.Show("اسم الوردية لا يمكن أن يكون فارغاً", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShiftData.Rules.Clear();
            foreach (var punchTime in PunchTimes)
            {
                if (TimeSpan.TryParse(punchTime.TimeValue, out TimeSpan time))
                {
                    ShiftData.Rules.Add(time);
                }
                else
                {
                    MessageBox.Show($"صيغة وقت غير صالحة: '{punchTime.TimeValue}'. الرجاء استخدام HH:mm", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (ShiftData.Rules.Count < 2)
            {
                MessageBox.Show("يجب أن تحتوي الوردية على وقت بداية ونهاية على الأقل", "خطأ في التحقق", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShiftData.ShiftName = ShiftNameTextBox.Text;
            ShiftData.StartTime = ShiftData.Rules.Min();
            ShiftData.EndTime = ShiftData.Rules.Max();

            this.DialogResult = true;
        }
    }
}
