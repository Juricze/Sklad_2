using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReceiptPreviewDialog : ContentDialog, INotifyPropertyChanged
    {
        public Receipt Receipt { get; }
        private readonly IPrintService _printService;

        private decimal _receivedAmount;
        public decimal ReceivedAmount
        {
            get => _receivedAmount;
            set => SetProperty(ref _receivedAmount, value);
        }

        private decimal _changeAmount;
        public decimal ChangeAmount
        {
            get => _changeAmount;
            set => SetProperty(ref _changeAmount, value);
        }

        public ObservableCollection<VatSummary> VatSummaries { get; } = new();

        /// <summary>
        /// Částka k úhradě po odečtení dárkového poukazu
        /// </summary>
        public decimal AmountToPay => Receipt.TotalAmount - (Receipt.ContainsGiftCardRedemption ? Receipt.GiftCardRedemptionAmount : 0);

        public event PropertyChangedEventHandler PropertyChanged;

        public ReceiptPreviewDialog(Receipt receipt, IPrintService printService)
        {
            this.InitializeComponent();
            Receipt = receipt;
            _printService = printService;
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

        private async void PrintButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // Dialog se nezavře po kliknutí na Tisk

            try
            {
                var printSuccess = await _printService.PrintReceiptAsync(Receipt);

                if (!printSuccess)
                {
                    Debug.WriteLine("Warning: Receipt print failed - printer may not be connected");

                    // Show error dialog
                    var errorDialog = new ContentDialog
                    {
                        Title = "Chyba tisku",
                        Content = "Tisk se nezdařil. Zkontrolujte připojení tiskárny a zkuste to znovu.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync().AsTask();
                }
                else
                {
                    Debug.WriteLine($"Receipt {Receipt.FormattedReceiptNumber} printed successfully from preview dialog");
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Exception during receipt printing from preview: {ex.Message}");

                // Show error dialog
                var errorDialog = new ContentDialog
                {
                    Title = "Chyba tisku",
                    Content = $"Nastala chyba při tisku: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync().AsTask();
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