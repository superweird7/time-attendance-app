using System;
using System.Windows;
using System.Windows.Media;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// Manages global font size settings for the application using scale transform.
    /// </summary>
    public static class FontSizeManager
    {
        private static double _currentScale = 1.0;

        public const double MinScale = 0.8;  // 80% - smallest
        public const double MaxScale = 1.4;  // 140% - largest
        public const double ScaleStep = 0.1; // 10% increments

        public static double CurrentScale
        {
            get => _currentScale;
            private set => _currentScale = Math.Max(MinScale, Math.Min(MaxScale, Math.Round(value, 1)));
        }

        public static event EventHandler FontSizeChanged;

        /// <summary>
        /// Increases the font size by one step.
        /// </summary>
        public static void Increase()
        {
            if (CurrentScale < MaxScale)
            {
                CurrentScale += ScaleStep;
                ApplyToAllWindows();
                FontSizeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Decreases the font size by one step.
        /// </summary>
        public static void Decrease()
        {
            if (CurrentScale > MinScale)
            {
                CurrentScale -= ScaleStep;
                ApplyToAllWindows();
                FontSizeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Resets the font size to default.
        /// </summary>
        public static void Reset()
        {
            CurrentScale = 1.0;
            ApplyToAllWindows();
            FontSizeChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the display percentage string.
        /// </summary>
        public static string GetDisplayPercentage()
        {
            return $"{(int)(CurrentScale * 100)}%";
        }

        /// <summary>
        /// Applies the current scale to all open windows.
        /// </summary>
        private static void ApplyToAllWindows()
        {
            var app = Application.Current;
            if (app == null) return;

            foreach (Window window in app.Windows)
            {
                ApplyToWindow(window);
            }
        }

        /// <summary>
        /// Applies the current scale transform to a specific window.
        /// Call this when opening new windows to apply the current scale.
        /// </summary>
        public static void ApplyToWindow(Window window)
        {
            if (window == null) return;

            try
            {
                // Apply scale transform to window content
                if (window.Content is FrameworkElement content)
                {
                    content.LayoutTransform = new ScaleTransform(_currentScale, _currentScale);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
