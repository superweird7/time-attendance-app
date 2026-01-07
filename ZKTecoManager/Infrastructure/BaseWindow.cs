using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// Base window class providing common functionality for all windows.
    /// Implements IDisposable for proper resource cleanup.
    /// </summary>
    public class BaseWindow : Window, IDisposable
    {
        private bool _disposed = false;

        public BaseWindow()
        {
            // Register for window closing to ensure cleanup
            Closing += BaseWindow_Closing;
        }

        /// <summary>
        /// Handles window title bar dragging.
        /// </summary>
        protected void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Handles close button click.
        /// </summary>
        protected void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles cancel button click.
        /// </summary>
        protected void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Override in derived classes to perform cleanup.
        /// </summary>
        protected virtual void OnCleanup()
        {
        }

        private void BaseWindow_Closing(object sender, CancelEventArgs e)
        {
            OnCleanup();
        }

        protected override void OnClosed(EventArgs e)
        {
            OnCleanup();
            base.OnClosed(e);
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    OnCleanup();
                }
                _disposed = true;
            }
        }

        ~BaseWindow()
        {
            Dispose(false);
        }

        #endregion
    }
}
