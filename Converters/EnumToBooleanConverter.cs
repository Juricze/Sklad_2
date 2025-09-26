using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Sklad_2.Models;
using System;

namespace Sklad_2.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is not string enumString)
            {
                return false;
            }

            if (value == null || !Enum.IsDefined(value.GetType(), value))
            {
                return false;
            }

            var enumValue = Enum.Parse(value.GetType(), enumString);
            return value.Equals(enumValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is not true)
            {
                return DependencyProperty.UnsetValue;
            }

            if (parameter is not string enumString)
            {
                return DependencyProperty.UnsetValue;
            }
            
            return Enum.Parse(typeof(DateFilterType), enumString);
        }
    }
}
