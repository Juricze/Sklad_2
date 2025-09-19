using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReceiptPreviewDialog : ContentDialog
    {
        public Receipt Receipt { get; }

        public ReceiptPreviewDialog(Receipt receipt)
        {
            this.InitializeComponent();
            Receipt = receipt;
        }
    }
}
