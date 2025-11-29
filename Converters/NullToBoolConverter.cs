using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    /// <summary>
    /// Converts null to false, non-null to true
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
