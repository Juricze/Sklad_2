using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sklad_2.Models
{
    public class VatSummary : INotifyPropertyChanged
    {
        private decimal _vatRate;
        public decimal VatRate
        {
            get => _vatRate;
            set => SetProperty(ref _vatRate, value);
        }

        private decimal _totalAmountWithoutVat;
        public decimal TotalAmountWithoutVat
        {
            get => _totalAmountWithoutVat;
            set => SetProperty(ref _totalAmountWithoutVat, value);
        }

        private decimal _totalVatAmount;
        public decimal TotalVatAmount
        {
            get => _totalVatAmount;
            set => SetProperty(ref _totalVatAmount, value);
        }

        public string VatRateFormatted => $"{VatRate} %";
        public string TotalAmountWithoutVatFormatted => TotalAmountWithoutVat.ToString("C");
        public string TotalVatAmountFormatted => TotalVatAmount.ToString("C");

        public event PropertyChangedEventHandler PropertyChanged;

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