using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
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

        public event PropertyChangedEventHandler PropertyChanged;

        public ReturnPreviewDialog(Return returnDocument)
        {
            this.InitializeComponent();
            ReturnDocument = returnDocument;
            CalculateVatSummary();
            this.DataContext = this;
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
