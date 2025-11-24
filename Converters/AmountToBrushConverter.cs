using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace Sklad_2.Converters
{
    /// <summary>
    /// Converter for amount coloring:
    /// - Negative = Red
    /// - Zero = Black (default)
    /// - Positive = Green
    /// </summary>
    public class AmountToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal amount)
            {
                if (amount < 0)
                {
                    // Negative = Red
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 59, 48)); // #FF3B30
                }
                else if (amount > 0)
                {
                    // Positive = Green
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 199, 89)); // #34C759
                }
                else
                {
                    // Zero = Black (explicit)
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)); // #000000
                }
            }

            // Null = default (black)
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
