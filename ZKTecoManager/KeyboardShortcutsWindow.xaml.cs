using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager
{
    public partial class KeyboardShortcutsWindow : Window
    {
        public KeyboardShortcutsWindow()
        {
            InitializeComponent();
            this.FlowDirection = (Thread.CurrentThread.CurrentUICulture.Name == "ar-IQ") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            this.KeyDown += Window_KeyDown;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.F9)
            {
                this.Close();
                e.Handled = true;
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
    }
}
