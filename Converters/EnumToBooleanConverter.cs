using Microsoft.UI.Xaml.Data;
using Sklad_2.ViewModels;
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

            if (!Enum.IsDefined(typeof(DateFilterType), value))
            {
                return false;
            }

            var enumValue = Enum.Parse(typeof(DateFilterType), enumString);
            return value.Equals(enumValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (parameter is not string enumString)
            {
                return null;
            }

            return Enum.Parse(typeof(DateFilterType), enumString);
        }
    }
}
