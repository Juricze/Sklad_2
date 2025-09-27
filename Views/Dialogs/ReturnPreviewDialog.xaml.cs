using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReturnPreviewDialog : ContentDialog
    {
        public Return ReturnDocument { get; }
        public ObservableCollection<VatSummary> VatSummaries { get; } = new();

        public ReturnPreviewDialog(Return returnDocument)
        {
            this.InitializeComponent();
            ReturnDocument = returnDocument;
            CalculateVatSummary();
        }

        private void CalculateVatSummary()
        {
            if (ReturnDocument?.Items == null) return;

            var summary = ReturnDocument.Items
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
    }
}
