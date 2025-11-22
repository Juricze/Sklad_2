using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class PrijemZboziViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IAuthService _authService;

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

        public PrijemZboziViewModel(IDataService dataService, IAuthService authService)
        {
            _dataService = dataService;
            _authService = authService;
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
        private async Task AddToStockAsync()
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

            if (stockQuantityValue <= 0)
            {
                StatusMessage = "Počet kusů musí být větší než 0.";
                return;
            }

            try
            {
                // Use FoundProduct directly, as it should already be loaded
                int stockBefore = FoundProduct.StockQuantity;
                FoundProduct.StockQuantity += stockQuantityValue;
                int stockAfter = FoundProduct.StockQuantity;

                await _dataService.UpdateProductAsync(FoundProduct);

                // Record stock movement - Stock In
                var stockMovement = new StockMovement
                {
                    ProductEan = FoundProduct.Ean,
                    ProductName = FoundProduct.Name,
                    MovementType = StockMovementType.StockIn,
                    QuantityChange = stockQuantityValue,
                    StockBefore = stockBefore,
                    StockAfter = stockAfter,
                    Timestamp = DateTime.Now,
                    UserName = _authService.CurrentUser?.DisplayName ?? "Systém",
                    Notes = $"Naskladnění +{stockQuantityValue} ks"
                };
                await _dataService.AddStockMovementAsync(stockMovement);

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
