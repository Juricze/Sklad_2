using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReturnPreviewDialog : ContentDialog, INotifyPropertyChanged
    {
        public Return ReturnDocument { get; }
        public ObservableCollection<VatSummary> VatSummaries { get; } = new();
        public bool IsVatPayer => ReturnDocument?.IsVatPayer ?? false;
        public string OriginalReceiptNumber { get; private set; }

        private readonly IPrintService _printService;
        private readonly IDataService _dataService;

        public event PropertyChangedEventHandler PropertyChanged;

        public ReturnPreviewDialog(Return returnDocument, IPrintService printService, IDataService dataService = null)
        {
            this.InitializeComponent();
            ReturnDocument = returnDocument;
            _printService = printService;
            _dataService = dataService;
            CalculateVatSummary();
            LoadOriginalReceiptNumber();
            this.DataContext = this;
        }

        private async void LoadOriginalReceiptNumber()
        {
            if (_dataService != null && ReturnDocument.OriginalReceiptId > 0)
            {
                var originalReceipt = await _dataService.GetReceiptByIdAsync(ReturnDocument.OriginalReceiptId);
                if (originalReceipt != null)
                {
                    OriginalReceiptNumber = originalReceipt.FormattedReceiptNumber;
                }
                else
                {
                    OriginalReceiptNumber = ReturnDocument.OriginalReceiptId.ToString();
                }
            }
            else
            {
                OriginalReceiptNumber = ReturnDocument.OriginalReceiptId.ToString();
            }
            OnPropertyChanged(nameof(OriginalReceiptNumber));
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

        private async void PrintButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Defer closing until we complete the print
            var deferral = args.GetDeferral();

            try
            {
                var success = await _printService.PrintReturnAsync(ReturnDocument);

                if (!success)
                {
                    // Show error but keep dialog open
                    args.Cancel = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
