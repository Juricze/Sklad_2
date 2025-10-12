using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is not string enumString)
            {
                return Visibility.Collapsed;
            }

            if (value == null || !Enum.IsDefined(value.GetType(), value))
            {
                return Visibility.Collapsed;
            }

            var enumValue = Enum.Parse(value.GetType(), enumString);
            return value.Equals(enumValue) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
