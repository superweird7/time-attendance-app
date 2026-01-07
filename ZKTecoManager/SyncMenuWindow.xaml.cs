using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager
{
    public partial class SyncMenuWindow : Window
    {
        private Machine _selectedMachine;

        public SyncMenuWindow(Machine selectedMachine)
        {
            InitializeComponent();
            _selectedMachine = selectedMachine;
        }

        // Allow dragging window
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

        private void FromDeviceToPC_Click(object sender, RoutedEventArgs e)
        {
            var window = new FromDeviceToPCWindow(_selectedMachine);
            window.Owner = this;
            window.ShowDialog();
        }

        private void FromPCToDevice_Click(object sender, RoutedEventArgs e)
        {
            var window = new FromPCToDeviceWindow();
            window.Owner = this;
            window.ShowDialog();
        }
    }
}
