using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class CashPaymentDialog : ContentDialog, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public decimal GrandTotal { get; }

        private decimal _receivedAmount;
        public decimal ReceivedAmount
        {
            get => _receivedAmount;
            set
            {
                if (_receivedAmount != value)
                {
                    _receivedAmount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ChangeAmount));
                    ErrorMessage = string.Empty; // Clear error message on input change
                }
            }
        }

        public decimal ChangeAmount => ReceivedAmount - GrandTotal;

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public CashPaymentDialog(decimal grandTotal)
        {
            this.InitializeComponent();
            GrandTotal = grandTotal;
            ReceivedAmount = 0m; // Nastavíme výchozí hodnotu ReceivedAmount na 0m
            this.DataContext = this;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ReceivedAmount < GrandTotal)
            {
                args.Cancel = true;
                ErrorMessage = "Přijatá hotovost je nižší než celková částka k úhradě.";
            }
            else
            {
                ErrorMessage = string.Empty; // Clear error if valid
            }
            await Task.CompletedTask; // Vyřešení upozornění CS1998
        }
    }
}