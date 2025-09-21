using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class PrijemZboziViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private string ean = string.Empty;

        [ObservableProperty]
        private string stockQuantity = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProductFound))]
        [NotifyCanExecuteChangedFor(nameof(AddToStockCommand))]
        private Product foundProduct;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public bool IsProductFound => FoundProduct != null;

        public PrijemZboziViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        [RelayCommand]
        private async Task FindProductDetailsAsync()
        {
            StatusMessage = string.Empty;
            FoundProduct = null; // Clear previous product details

            if (string.IsNullOrWhiteSpace(Ean))
            {
                StatusMessage = "Zadejte prosím EAN kód.";
                return;
            }

            try
            {
                var product = await _dataService.GetProductAsync(Ean);
                if (product != null)
                {
                    FoundProduct = product;
                    StatusMessage = $"Produkt '{product.Name}' nalezen.";
                }
                else
                {
                    StatusMessage = $"Produkt s EAN kódem '{Ean}' nebyl nalezen. Pro přidání nového produktu použijte stránku Nový produkt.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při vyhledávání produktu: {ex.Message}";
            }
        }

        private bool CanAddToStock() => FoundProduct != null;

        [RelayCommand(CanExecute = nameof(CanAddToStock))]
        private void AddToStock()
        {
            StatusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(StockQuantity))
            {
                StatusMessage = "Počet kusů musí být vyplněn.";
                return;
            }

            if (!int.TryParse(StockQuantity, out int stockQuantityValue))
            {
                StatusMessage = "Počet kusů musí být platné číslo.";
                return;
            }

            try
            {
                // Use FoundProduct directly, as it should already be loaded
                FoundProduct.StockQuantity += stockQuantityValue;
                _dataService.UpdateProductAsync(FoundProduct);
                StatusMessage = $"Skladové zásoby produktu '{FoundProduct.Name}' (EAN: {FoundProduct.Ean}) byly navýšeny o {stockQuantityValue} ks. Nový stav: {FoundProduct.StockQuantity} ks.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při naskladnění: {ex.Message}";
            }

            // Clear fields after successful stocking
            Ean = string.Empty;
            StockQuantity = string.Empty;
            FoundProduct = null; // Clear product details after stocking
        }

        [RelayCommand]
        private void ClearForm()
        {
            Ean = string.Empty;
            StockQuantity = string.Empty;
            FoundProduct = null;
            StatusMessage = string.Empty;
        }
    }
}
