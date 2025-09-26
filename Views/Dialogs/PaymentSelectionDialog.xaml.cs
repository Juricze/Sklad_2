using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class PaymentSelectionDialog : ContentDialog
    {
        public decimal GrandTotal { get; }
        public PaymentMethod SelectedPaymentMethod { get; private set; }

        public PaymentSelectionDialog(decimal grandTotal)
        {
            this.InitializeComponent();
            GrandTotal = grandTotal;
            SelectedPaymentMethod = PaymentMethod.None;
            this.DataContext = this;
        }

        private void CashButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SelectedPaymentMethod = PaymentMethod.Cash;
            this.Hide();
        }

        private void CardButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SelectedPaymentMethod = PaymentMethod.Card;
            this.Hide();
        }
    }
}