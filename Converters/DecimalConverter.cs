using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace Sklad_2.Converters
{
    public class DecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal decimalValue)
            {
                if (decimalValue == 0m)
                {
                    return string.Empty;
                }
                return decimalValue.ToString(CultureInfo.CurrentCulture);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringValue)
            {
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return 0m;
                }

                if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal resultCurrentCulture))
                {
                    return resultCurrentCulture;
                }

                if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal resultInvariantCulture))
                {
                    return resultInvariantCulture;
                }
            }
            return Microsoft.UI.Xaml.DependencyProperty.UnsetValue;
        }
    }
}