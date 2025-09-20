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
        private decimal totalSalesAmount;

        [ObservableProperty]
        private decimal totalVatAmount;

        [ObservableProperty]
        private int numberOfReceipts;

        public PrehledProdejuViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        [RelayCommand]
        private async Task LoadSalesDataAsync()
        {
            Sales.Clear();
            var allReceipts = await _dataService.GetReceiptsAsync();

            // For now, load all receipts. Filtering by date range will be added later.
            foreach (var receipt in allReceipts.OrderByDescending(r => r.SaleDate))
            {
                Sales.Add(receipt);
            }

            CalculateTotals();
        }

        private void CalculateTotals()
        {
            TotalSalesAmount = Sales.Sum(r => r.TotalAmount);
            TotalVatAmount = Sales.Sum(r => r.TotalVatAmount);
            NumberOfReceipts = Sales.Count;
        }
    }
}
