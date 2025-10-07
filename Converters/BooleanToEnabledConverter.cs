using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class BooleanToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                // If parameter is "Inverse", invert the boolean value
                if (parameter as string == "Inverse")
                {
                    return !boolValue;
                }
                return boolValue;
            }
            return true; // Default to enabled if value is not a boolean
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
