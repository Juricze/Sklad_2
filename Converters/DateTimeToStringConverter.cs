using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dateTime)
            {
                // Default format if no parameter is provided
                string format = parameter as string ?? "dd.MM.yyyy HH:mm:ss";
                return dateTime.ToString(format);
            }
            return value; // Return original value if not DateTime
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
