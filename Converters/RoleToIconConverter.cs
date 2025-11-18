using Microsoft.UI.Xaml.Data;
using System;

namespace Sklad_2.Converters
{
    public class RoleToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string role)
            {
                return role switch
                {
                    "Admin" => "\uE7EF", // Admin icon
                    "Cashier" => "\uE77B", // Contact icon
                    _ => "\uE77B" // Default contact icon
                };
            }
            return "\uE77B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
