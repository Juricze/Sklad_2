using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class PaymentMethodToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string paymentMethod && parameter is string targetPaymentMethod)
            {
                // Use Contains to match "Hotově" in "Hotově + Dárkový poukaz"
                return paymentMethod.Contains(targetPaymentMethod) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
