using Sklad_2.Models;

namespace Sklad_2.ViewModels
{
    public class CashRegisterEntryViewModel
    {
        public CashRegisterEntry Entry { get; }

        public string TypeAsString
        {
            get
            {
                switch (Entry.Type)
                {
                    case EntryType.DayStart:
                        return "Zahájení dne";
                    case EntryType.Sale:
                        return "Prodej";
                    case EntryType.Withdrawal:
                        return "Výběr";
                    case EntryType.Deposit:
                        return "Vklad";
                    case EntryType.DailyReconciliation:
                        return "Denní kontrola";
                    case EntryType.Return:
                        return "Vratka";
                    case EntryType.DayClose:
                        return "Uzavření dne";
                    default:
                        return Entry.Type.ToString();
                }
            }
        }

        public decimal AmountToDisplay
        {
            get
            {
                if (Entry.Type == EntryType.DailyReconciliation || Entry.Type == EntryType.Return)
                {
                    return -Entry.Amount;
                }
                return Entry.Amount;
            }
        }

        public bool IsDeficit
        {
            get
            {
                return (Entry.Type == EntryType.DailyReconciliation || Entry.Type == EntryType.Return) && Entry.Amount > 0;
            }
        }

        public CashRegisterEntryViewModel(CashRegisterEntry entry)
        {
            Entry = entry;
        }
    }
}