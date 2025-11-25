using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace Sklad_2.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                string parameterString = parameter?.ToString();
                
                if (parameterString == "BackupPath")
                {
                    // For backup path: true = blue (configured), false = red (not configured)
                    return new SolidColorBrush(boolValue ? Microsoft.UI.ColorHelper.FromArgb(255, 0, 122, 255) : Colors.Red);
                }
                else
                {
                    // Default behavior: Error = Red, Success = Green
                    return new SolidColorBrush(boolValue ? Colors.Red : Colors.Green);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
