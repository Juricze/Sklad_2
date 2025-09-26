using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReceiptPreviewDialog : ContentDialog
    {
        public Receipt Receipt { get; }
        public decimal ReceivedAmount { get; }
        public decimal ChangeAmount { get; }

        public ReceiptPreviewDialog(Receipt receipt)
        {
            this.InitializeComponent();
            Receipt = receipt;
            ReceivedAmount = receipt.ReceivedAmount;
            ChangeAmount = receipt.ChangeAmount;
            this.DataContext = this;
        }

        private void PrintButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Zde bude v budoucnu logika pro tisk účtenky
            // TODO: Implementovat tisk účtenky
            args.Cancel = true; // Dialog se nezavře po kliknutí na Tisk
        }
    }
}