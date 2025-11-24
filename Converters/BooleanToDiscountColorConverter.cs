using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace Sklad_2.Converters
{
    public class BooleanToDiscountColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isDiscounted && isDiscounted)
            {
                return new SolidColorBrush(Colors.Gray);
            }
            
            // Return default text color
            return Application.Current.Resources["TextFillColorPrimaryBrush"] as SolidColorBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}