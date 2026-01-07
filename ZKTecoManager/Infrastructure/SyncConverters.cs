using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// Converts boolean to color for active/inactive status display.
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) // Green #10B981
                               : new SolidColorBrush(Color.FromRgb(239, 68, 68));   // Red #EF4444
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to status text (Active/Inactive in Arabic).
    /// </summary>
    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "نشط" : "غير نشط";
            }
            return "غير معروف";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
