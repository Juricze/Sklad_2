using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Sklad_2.Models;
using System;

namespace Sklad_2.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is not string enumString)
            {
                return false;
            }

            if (value == null)
            {
                return false;
            }

            var enumType = value.GetType();
            if (!enumType.IsEnum)
            {
                return false;
            }

            try
            {
                var enumValue = Enum.Parse(enumType, enumString);
                return value.Equals(enumValue);
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is not true)
            {
                return DependencyProperty.UnsetValue;
            }

            if (parameter is not string enumString)
            {
                return DependencyProperty.UnsetValue;
            }

            try
            {
                // Try DateFilterType first (most common case)
                if (Enum.IsDefined(typeof(DateFilterType), enumString))
                {
                    return Enum.Parse(typeof(DateFilterType), enumString);
                }

                // If targetType is provided and is an enum, use it
                if (targetType != null && targetType.IsEnum)
                {
                    return Enum.Parse(targetType, enumString);
                }

                return DependencyProperty.UnsetValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnumToBooleanConverter.ConvertBack error: targetType={targetType?.Name}, parameter={parameter}, ex={ex.Message}");
                return DependencyProperty.UnsetValue;
            }
        }
    }
}
