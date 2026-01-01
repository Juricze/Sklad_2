using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Sklad_2.Models;
using System;

namespace Sklad_2.Converters
{
    /// <summary>
    /// Converter pro barvu částky "Celkem" v seznamu účtenek
    /// - Fialová (#A855F7) pokud obsahuje individuální slevy na produkty
    /// - Jinak zelená/červená/černá podle částky (logika AmountToBrushConverter)
    /// </summary>
    public class ReceiptTotalBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not Receipt receipt)
                return new SolidColorBrush(Colors.Black);

            // Pokud obsahuje individuální slevy → FIALOVÁ
            if (receipt.HasProductDiscounts)
            {
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 168, 85, 247)); // #A855F7 (fialová)
            }

            // Jinak použít logiku AmountToBrushConverter (podle částky)
            var amount = receipt.AmountToPay;

            if (amount < 0)
            {
                // Záporná částka (vratka) → Červená
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 59, 48)); // #FF3B30
            }
            else if (amount > 0)
            {
                // Kladná částka (normální účtenka) → Zelená
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 199, 89)); // #34C759
            }
            else
            {
                // Nulová částka → Černá
                return new SolidColorBrush(Colors.Black);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
