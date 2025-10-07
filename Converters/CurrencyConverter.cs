using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal amount)
            {
                return $"{amount:C}";
            }
            // If the value is not a decimal, and the targetType is string, return an empty string.
            if (targetType == typeof(string))
            {
                return string.Empty;
            }
            // If the value is not a decimal and targetType is not string, return DependencyProperty.UnsetValue to indicate that the converter produced no value.
            return Microsoft.UI.Xaml.DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}