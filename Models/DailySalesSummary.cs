using System;

namespace Sklad_2.Models
{
    /// <summary>
    /// Souhrn denních tržeb pro přehled v Tržby/Uzavírky
    /// </summary>
    public class DailySalesSummary
    {
        public DateTime Date { get; set; }

        public string ReceiptRangeFrom { get; set; }
        public string ReceiptRangeTo { get; set; }

        public string ReturnRangeFrom { get; set; }
        public string ReturnRangeTo { get; set; }

        public decimal CashSales { get; set; }
        public decimal CardSales { get; set; }
        public decimal TotalSales { get; set; }

        public int ReceiptCount { get; set; }

        // Formatted properties pro UI
        public string DateFormatted => Date.ToString("dd.MM.yyyy");
        public string ReceiptRange => string.IsNullOrEmpty(ReceiptRangeFrom)
            ? "—"
            : $"{ReceiptRangeFrom} - {ReceiptRangeTo}";
        public string ReturnRange => string.IsNullOrEmpty(ReturnRangeFrom)
            ? "—"
            : $"{ReturnRangeFrom} - {ReturnRangeTo}";
        public string CashSalesFormatted => $"{CashSales:N2} Kč";
        public string CardSalesFormatted => $"{CardSales:N2} Kč";
        public string TotalSalesFormatted => $"{TotalSales:N2} Kč";
    }
}
