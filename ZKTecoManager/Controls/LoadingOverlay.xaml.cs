using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZKTecoManager.Controls
{
    /// <summary>
    /// Reusable loading overlay control with spinner animation.
    /// Blocks all user interactions while visible.
    /// </summary>
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
        }

        #region Event Handlers to Block Interactions

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Block the click
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Block the click
        }

        private void Grid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Block the click before it reaches children
        }

        private void Grid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Block the click before it reaches children
        }

        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true; // Block keyboard input
        }

        #endregion

        #region Dependency Properties

        /// <summary>
        /// The main loading message text.
        /// </summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(LoadingOverlay),
                new PropertyMetadata("Loading...", OnMessageChanged));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (LoadingOverlay)d;
            control.LoadingText.Text = e.NewValue?.ToString() ?? "Loading...";
        }

        /// <summary>
        /// Optional status text shown below the main message.
        /// </summary>
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(string), typeof(LoadingOverlay),
                new PropertyMetadata("", OnStatusChanged));

        public string Status
        {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (LoadingOverlay)d;
            control.StatusText.Text = e.NewValue?.ToString() ?? "";
        }

        /// <summary>
        /// Whether to show the progress bar.
        /// </summary>
        public static readonly DependencyProperty ShowProgressProperty =
            DependencyProperty.Register("ShowProgress", typeof(bool), typeof(LoadingOverlay),
                new PropertyMetadata(false, OnShowProgressChanged));

        public bool ShowProgress
        {
            get => (bool)GetValue(ShowProgressProperty);
            set => SetValue(ShowProgressProperty, value);
        }

        private static void OnShowProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (LoadingOverlay)d;
            control.ProgressBar.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Progress value (0-100).
        /// </summary>
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(LoadingOverlay),
                new PropertyMetadata(0.0, OnProgressChanged));

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (LoadingOverlay)d;
            control.ProgressBar.Value = (double)e.NewValue;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the loading overlay.
        /// </summary>
        public void Show(string message = "Loading...", string status = "")
        {
            Message = message;
            Status = status;
            Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the loading overlay.
        /// </summary>
        public void Hide()
        {
            Visibility = Visibility.Collapsed;
            Status = "";
            ShowProgress = false;
            Progress = 0;
        }

        /// <summary>
        /// Updates the status text while loading.
        /// </summary>
        public void UpdateStatus(string status)
        {
            Status = status;
        }

        /// <summary>
        /// Updates the progress value.
        /// </summary>
        public void UpdateProgress(double progress, string status = null)
        {
            ShowProgress = true;
            Progress = progress;
            if (status != null)
            {
                Status = status;
            }
        }

        #endregion
    }
}
