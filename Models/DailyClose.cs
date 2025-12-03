using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    /// <summary>
    /// Denní uzavírka - záznam tržeb za jeden obchodní den
    /// </summary>
    public partial class DailyClose : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int id;

        /// <summary>
        /// Datum obchodního dne (bez času)
        /// </summary>
        [ObservableProperty]
        private DateTime date;

        /// <summary>
        /// Tržba v hotovosti (včetně vratek)
        /// </summary>
        [ObservableProperty]
        private decimal cashSales;

        /// <summary>
        /// Tržba kartou
        /// </summary>
        [ObservableProperty]
        private decimal cardSales;

        /// <summary>
        /// Celková tržba (CashSales + CardSales)
        /// </summary>
        [ObservableProperty]
        private decimal totalSales;

        /// <summary>
        /// Celková částka DPH (pouze pokud je plátce DPH)
        /// </summary>
        [ObservableProperty]
        private decimal? vatAmount;

        /// <summary>
        /// Jméno prodavače který uzavřel den
        /// </summary>
        [ObservableProperty]
        private string sellerName;

        /// <summary>
        /// První číslo účtenky v rozmezí (U0001/2025)
        /// </summary>
        [ObservableProperty]
        private string receiptNumberFrom;

        /// <summary>
        /// Poslední číslo účtenky v rozmezí (U0025/2025)
        /// </summary>
        [ObservableProperty]
        private string receiptNumberTo;

        /// <summary>
        /// Časové razítko uzavření
        /// </summary>
        [ObservableProperty]
        private DateTime closedAt;

        // Formatted properties pro UI
        public string DateFormatted => Date.ToString("dd.MM.yyyy");
        public string CashSalesFormatted => $"{CashSales:N2} Kč";
        public string CardSalesFormatted => $"{CardSales:N2} Kč";
        public string TotalSalesFormatted => $"{TotalSales:N2} Kč";
        public string VatAmountFormatted => VatAmount.HasValue ? $"{VatAmount.Value:N2} Kč" : "-";
        public string ReceiptRange => $"{ReceiptNumberFrom} - {ReceiptNumberTo}";
    }
}
