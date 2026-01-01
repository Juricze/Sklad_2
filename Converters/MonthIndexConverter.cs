using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class MonthIndexConverter : IValueConverter
    {
        // ViewModel → UI: Month (1-12) → Index (0-11)
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int month && month >= 1 && month <= 12)
            {
                return month - 1; // leden (1) → index 0
            }
            return 0;
        }

        // UI → ViewModel: Index (0-11) → Month (1-12)
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is int index && index >= 0 && index <= 11)
            {
                return index + 1; // index 0 → leden (1)
            }
            return 1;
        }
    }
}
