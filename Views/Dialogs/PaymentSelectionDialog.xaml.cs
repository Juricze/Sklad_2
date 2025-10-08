using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class PaymentSelectionDialog : ContentDialog, INotifyPropertyChanged
    {
        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set => SetProperty(ref _grandTotal, value);
        }
        public PaymentMethod SelectedPaymentMethod { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

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