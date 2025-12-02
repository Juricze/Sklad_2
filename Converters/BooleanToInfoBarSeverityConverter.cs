using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class BooleanToInfoBarSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isError)
            {
                return isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            }
            return InfoBarSeverity.Informational;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
