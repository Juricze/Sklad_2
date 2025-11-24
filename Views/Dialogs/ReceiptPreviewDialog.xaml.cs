using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReceiptPreviewDialog : ContentDialog, INotifyPropertyChanged
    {
        public Receipt Receipt { get; }

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