using System;
using System.Globalization;
using System.Windows.Data;

namespace VietTravel.UI.Converters
{
    /// <summary>
    /// Compares bound string value with ConverterParameter, returns true if they match.
    /// Used for sidebar RadioButton IsChecked binding.
    /// </summary>
    public class StringMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
                return parameter.ToString()!;
            return Binding.DoNothing;
        }
    }
}
