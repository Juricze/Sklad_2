using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class DayClosedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isClosed)
            {
                return isClosed ? "🔒 Den uzavřen" : "🔓 Den otevřen";
            }
            return "Stav neznámý";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
