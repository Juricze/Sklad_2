using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class NaskladneniViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private string ean;

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string salePrice = string.Empty;

        [ObservableProperty]
        private string stockQuantity = string.Empty;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public NaskladneniViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        [RelayCommand]
        private async Task AddProductAsync()
        {
            StatusMessage = string.Empty; // Clear previous message

            if (string.IsNullOrWhiteSpace(Ean) || string.IsNullOrWhiteSpace(Name))
            {
                StatusMessage = "EAN a Název produktu nesmí být prázdné.";
                return;
            }

            decimal.TryParse(SalePrice, out decimal salePriceValue);
            int.TryParse(StockQuantity, out int stockQuantityValue);

            var newProduct = new Product
            {
                Ean = this.Ean,
                Name = this.Name,
                SalePrice = salePriceValue,
                StockQuantity = stockQuantityValue,
                Category = "Default"
            };

            try
            {
                await _dataService.AddProductAsync(newProduct);
                StatusMessage = "Produkt úspěšně přidán!";
            }
            catch (Exception ex) // Catching generic Exception for now, can be more specific later
            {
                StatusMessage = $"Chyba při přidávání produktu: {ex.Message}";
            }


            Ean = string.Empty;
            Name = string.Empty;
            SalePrice = string.Empty;
            StockQuantity = string.Empty;
        }
    }
}
