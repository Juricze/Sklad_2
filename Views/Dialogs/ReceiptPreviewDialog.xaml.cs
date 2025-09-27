using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReceiptPreviewDialog : ContentDialog
    {
        public Receipt Receipt { get; }
        public decimal ReceivedAmount { get; }
        public decimal ChangeAmount { get; }
        public ObservableCollection<VatSummary> VatSummaries { get; } = new();

        public ReceiptPreviewDialog(Receipt receipt)
        {
            this.InitializeComponent();
            Receipt = receipt;
            ReceivedAmount = receipt.ReceivedAmount;
            ChangeAmount = receipt.ChangeAmount;
            this.DataContext = this;
            CalculateVatSummary();
        }

        private void CalculateVatSummary()
        {
            var summary = Receipt.Items
                .GroupBy(item => item.VatRate)
                .Select(group => new VatSummary
                {
                    VatRate = group.Key,
                    TotalAmountWithoutVat = group.Sum(item => item.PriceWithoutVat),
                    TotalVatAmount = group.Sum(item => item.VatAmount)
                })
                .OrderBy(s => s.VatRate);

            foreach (var summaryItem in summary)
            {
                VatSummaries.Add(summaryItem);
            }
        }

        private void PrintButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Zde bude v budoucnu logika pro tisk účtenky
            // TODO: Implementovat tisk účtenky
            args.Cancel = true; // Dialog se nezavře po kliknutí na Tisk
        }
    }

    public class VatSummary
    {
        public decimal VatRate { get; set; }
        public decimal TotalAmountWithoutVat { get; set; }
        public decimal TotalVatAmount { get; set; }

        public string VatRateFormatted => $"{VatRate} %";
        public string TotalAmountWithoutVatFormatted => TotalAmountWithoutVat.ToString("C");
        public string TotalVatAmountFormatted => TotalVatAmount.ToString("C");
    }
}