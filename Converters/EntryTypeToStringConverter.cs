using Microsoft.UI.Xaml.Data;
using Sklad_2.Models;
using System;

namespace Sklad_2.Converters
{
    public class EntryTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is EntryType entryType)
            {
                switch (entryType)
                {
                    case EntryType.InitialDeposit:
                        return "Počáteční vklad";
                    case EntryType.Sale:
                        return "Prodej";
                    case EntryType.Withdrawal:
                        return "Výběr";
                    case EntryType.Deposit:
                        return "Vklad";
                    case EntryType.DailyReconciliation:
                        return "Denní uzávěrka";
                    default:
                        return value.ToString();
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}