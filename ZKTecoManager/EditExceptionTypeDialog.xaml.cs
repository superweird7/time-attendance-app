using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager
{
    public partial class EditExceptionTypeDialog : Window
    {
        public string ExceptionName { get; private set; }

        public EditExceptionTypeDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ExceptionName = NameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(ExceptionName))
            {
                MessageBox.Show("الرجاء إدخال اسم الاستثناء", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
