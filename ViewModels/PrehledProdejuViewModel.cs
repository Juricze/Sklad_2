using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class PrehledProdejuViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private ObservableCollection<Receipt> sales = new ObservableCollection<Receipt>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalSalesAmountFormatted))]
        private decimal totalSalesAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalSalesAmountWithoutVatFormatted))]
        private decimal totalSalesAmountWithoutVat;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalVatAmountFormatted))]
        private decimal totalVatAmount;

        public string TotalSalesAmountFormatted => $"{TotalSalesAmount:C}";
        public string TotalSalesAmountWithoutVatFormatted => $"{TotalSalesAmountWithoutVat:C}";
        public string TotalVatAmountFormatted => $"{TotalVatAmount:C}";

        [ObservableProperty]
        private int numberOfReceipts;

        [ObservableProperty]
        private DateTimeOffset startDate;

        [ObservableProperty]
        private DateTimeOffset endDate;

        public PrehledProdejuViewModel(IDataService dataService)
        { 
            _dataService = dataService;
            var now = DateTime.Now;
            StartDate = new DateTimeOffset(new DateTime(now.Year, now.Month, 1));
            EndDate = new DateTimeOffset(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, TimeSpan.Zero);
            LoadSalesDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadSalesDataAsync()
        {
            Sales.Clear();
            var allReceipts = await _dataService.GetReceiptsAsync(StartDate.DateTime, EndDate.DateTime);

            foreach (var receipt in allReceipts.OrderByDescending(r => r.SaleDate))
            {
                Sales.Add(receipt);
            }

            CalculateTotals();
        }

        private void CalculateTotals()
        {
            TotalSalesAmount = Sales.Sum(r => r.TotalAmount);
            TotalSalesAmountWithoutVat = Sales.Sum(r => r.TotalAmountWithoutVat);
            TotalVatAmount = Sales.Sum(r => r.TotalVatAmount);
            NumberOfReceipts = Sales.Count;
        }
    }
}
