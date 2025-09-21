using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class CashConfirmationDialog : ContentDialog, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public decimal GrandTotal { get; }
        public decimal ReceivedAmount { get; }

        private decimal _changeAmount;
        public decimal ChangeAmount
        {
            get => _changeAmount;
            set
            {
                if (_changeAmount != value)
                {
                    _changeAmount = value;
                    OnPropertyChanged();
                }
            }
        }

        public CashConfirmationDialog(decimal grandTotal, decimal receivedAmount, decimal changeAmount)
        {
            this.InitializeComponent();
            GrandTotal = grandTotal;
            ReceivedAmount = receivedAmount;
            ChangeAmount = changeAmount;
            this.DataContext = this;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}