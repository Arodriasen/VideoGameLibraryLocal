using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VideoGameLibrary.Converters
{
    public class IntZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString() == "invert";
            bool isZero = value is int i && i == 0;
            return (isZero ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
