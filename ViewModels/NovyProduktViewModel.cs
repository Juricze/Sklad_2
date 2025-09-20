using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class NovyProduktViewModel : ObservableObject
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
        private string selectedCategory;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>
        {
            "Potraviny",
            "Drogerie",
            "Elektronika",
            "Ostatní"
        };

        public NovyProduktViewModel(IDataService dataService)
        {
            _dataService = dataService;
            SelectedCategory = Categories[3];
        }

        [RelayCommand]
        private async Task AddNewProductAsync()
        {
            StatusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Ean) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(SalePrice) || string.IsNullOrWhiteSpace(PurchasePrice))
            {
                StatusMessage = "Všechna pole kromě počtu kusů musí být vyplněna.";
                return;
            }

            if (!decimal.TryParse(SalePrice, out decimal salePriceValue) || !decimal.TryParse(PurchasePrice, out decimal purchasePriceValue))
            {
                StatusMessage = "Cena musí být platné číslo.";
                return;
            }

            var newProduct = new Product
            {
                Ean = this.Ean,
                Name = this.Name,
                Category = this.SelectedCategory,
                SalePrice = salePriceValue,
                PurchasePrice = purchasePriceValue,
                StockQuantity = 0, // New products start with 0 stock
                VatRate = 0.21m // Default VAT for now
            };

            try
            {
                var existingProduct = await _dataService.GetProductAsync(newProduct.Ean);
                if (existingProduct != null)
                {
                    StatusMessage = $"Produkt s EAN kódem '{newProduct.Ean}' již existuje. Použijte prosím stránku Příjem zboží pro naskladnění.";
                }
                else
                {
                    await _dataService.AddProductAsync(newProduct);
                    StatusMessage = "Nový produkt byl úspěšně přidán do databáze!";

                    // Clear fields
                    Ean = string.Empty;
                    Name = string.Empty;
                    SalePrice = string.Empty;
                    PurchasePrice = string.Empty;
                    SelectedCategory = Categories[3];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při přidávání produktu: {ex.Message}";
            }
        }
    }
}