using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VietTravel.UI.Converters
{
    /// <summary>
    /// Compares bound string value with ConverterParameter.
    /// Returns bool (for RadioButton IsChecked) or Visibility (for content panel visibility).
    /// </summary>
    public class StringMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return targetType == typeof(Visibility) ? Visibility.Collapsed : (object)false;

            bool isMatch = value.ToString() == parameter.ToString();

            if (targetType == typeof(Visibility))
                return isMatch ? Visibility.Visible : Visibility.Collapsed;

            return isMatch;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
                return parameter.ToString()!;
            return Binding.DoNothing;
        }
    }
}
