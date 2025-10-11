using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool boolValue = value is bool b && b;

            // If parameter is "Inverse", invert the boolean value
            if (parameter as string == "Inverse")
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
