using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class NaskladneniViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private string ean = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string salePrice = string.Empty;

        [ObservableProperty]
        private string purchasePrice = string.Empty;

        [ObservableProperty]
        private string stockQuantity = string.Empty;

        [ObservableProperty]
        private string selectedCategory;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public ObservableCollection<string> Categories { get; }

        public NaskladneniViewModel(IDataService dataService)
        {
            _dataService = dataService;
            Categories = new ObservableCollection<string>
            {
                "Potraviny",
                "Drogerie",
                "Elektronika",
                "Ostatní"
            };
            SelectedCategory = Categories[3];
        }

        [RelayCommand]
        private async Task AddProductAsync()
        {
            StatusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Ean) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(SalePrice) || string.IsNullOrWhiteSpace(PurchasePrice) || string.IsNullOrWhiteSpace(StockQuantity))
            {
                StatusMessage = "Všechna pole musí být vyplněna.";
                return;
            }

            if (!decimal.TryParse(SalePrice, out decimal salePriceValue) || !decimal.TryParse(PurchasePrice, out decimal purchasePriceValue) || !int.TryParse(StockQuantity, out int stockQuantityValue))
            {
                StatusMessage = "Cena a počet kusů musí být platná čísla.";
                return;
            }

            var newProduct = new Product
            {
                Ean = this.Ean,
                Name = this.Name,
                Category = this.SelectedCategory,
                SalePrice = salePriceValue,
                PurchasePrice = purchasePriceValue,
                StockQuantity = stockQuantityValue,
                VatRate = 0.21m // Default VAT for now
            };

            try
            {
                var existingProduct = await _dataService.GetProductAsync(newProduct.Ean);
                if (existingProduct != null)
                {
                    existingProduct.Name = newProduct.Name;
                    existingProduct.Category = newProduct.Category;
                    existingProduct.SalePrice = newProduct.SalePrice;
                    existingProduct.PurchasePrice = newProduct.PurchasePrice;
                    existingProduct.StockQuantity += newProduct.StockQuantity;
                    await _dataService.UpdateProductAsync(existingProduct);
                    StatusMessage = $"Skladové zásoby produktu '{newProduct.Name}' byly navýšeny o {newProduct.StockQuantity} ks.";
                }
                else
                {
                    await _dataService.AddProductAsync(newProduct);
                    StatusMessage = "Nový produkt byl úspěšně přidán do databáze!";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při operaci s databází: {ex.Message}";
            }

            // Clear fields
            Ean = string.Empty;
            Name = string.Empty;
            SalePrice = string.Empty;
            PurchasePrice = string.Empty;
            StockQuantity = string.Empty;
            SelectedCategory = Categories[3];
        }
    }
}