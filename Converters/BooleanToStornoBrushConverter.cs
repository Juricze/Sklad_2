using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace Sklad_2.Converters
{
    /// <summary>
    /// Converter for storno receipts - returns red brush for true, default for false
    /// </summary>
    public class BooleanToStornoBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isStorno && isStorno)
            {
                // Storno = Red
                return new SolidColorBrush(Colors.Red);
            }
            // Normal = default (null returns default TextBlock foreground)
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
