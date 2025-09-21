using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class ProdejViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;
        public IReceiptService Receipt { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProductFound))]
        [NotifyPropertyChangedFor(nameof(ScannedProductPriceFormatted))]
        private Product scannedProduct;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(IncrementQuantityCommand))]
        [NotifyCanExecuteChangedFor(nameof(DecrementQuantityCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
        private Sklad_2.Services.ReceiptItem selectedReceiptItem;

        [ObservableProperty]
        private Receipt lastCreatedReceipt;

        public bool IsCheckoutSuccessful { get; private set; }

        public bool IsProductFound => ScannedProduct != null;
        private bool CanManipulateItem => SelectedReceiptItem != null;

        public string ScannedProductPriceFormatted => ScannedProduct != null ? $"{ScannedProduct.SalePrice:C}" : string.Empty;
        public string GrandTotalFormatted => $"Celkem: {Receipt.GrandTotal:C}";

        public ProdejViewModel(IDataService dataService, IReceiptService receiptService, ISettingsService settingsService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            Receipt = receiptService;
            Receipt.Items.CollectionChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(GrandTotalFormatted));
                if (e.NewItems != null)
                {
                    foreach (Sklad_2.Services.ReceiptItem item in e.NewItems) 
                    {
                        item.PropertyChanged += (s, e) =>
                        {
                            OnPropertyChanged(nameof(GrandTotalFormatted));
                            DecrementQuantityCommand.NotifyCanExecuteChanged();
                            IncrementQuantityCommand.NotifyCanExecuteChanged();
                        };
                    }
                }
                DecrementQuantityCommand.NotifyCanExecuteChanged();
            };
        }

        public event EventHandler<Product> ProductOutOfStock;
        public event EventHandler<string> CheckoutFailed;

        [RelayCommand]
        private async Task FindProductAsync(string eanCode)
        {
            if (string.IsNullOrWhiteSpace(eanCode)) return;

            ScannedProduct = null;
            var product = await _dataService.GetProductAsync(eanCode);
            if (product != null)
            {
                ScannedProduct = product; // Show product details immediately

                var existingItem = Receipt.Items.FirstOrDefault(item => item.Product.Ean == product.Ean);
                int currentQuantityInCart = existingItem?.Quantity ?? 0;

                if (currentQuantityInCart < product.StockQuantity)
                {
                    Receipt.AddProduct(product);
                }
                else
                {
                    // Product is out of stock or at stock limit, raise an event to notify the view
                    ProductOutOfStock?.Invoke(this, product);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanManipulateItem))]
        private void RemoveItem()
        {
            if (SelectedReceiptItem != null)
            {
                Receipt.RemoveItem(SelectedReceiptItem);
            }
        }

        [RelayCommand]
        private void ClearReceipt()
        {
            Receipt.Clear();
        }

        [RelayCommand(CanExecute = nameof(CanDecrementQuantity))]
        private void DecrementQuantity()
        {
            if (SelectedReceiptItem != null && SelectedReceiptItem.Quantity > 1)
            {
                SelectedReceiptItem.Quantity--;
            }
        }

        private bool CanDecrementQuantity()
        {
            return SelectedReceiptItem != null && SelectedReceiptItem.Quantity > 1;
        }

        private bool CanIncrementQuantity()
        {
            return SelectedReceiptItem != null && SelectedReceiptItem.Quantity < SelectedReceiptItem.Product.StockQuantity;
        }

        [RelayCommand(CanExecute = nameof(CanIncrementQuantity))]
        private void IncrementQuantity()
        {
            if (SelectedReceiptItem != null)
            {
                SelectedReceiptItem.Quantity++;
            }
        }

        [RelayCommand]
        private async Task CheckoutAsync()
        {
            IsCheckoutSuccessful = false;
            LastCreatedReceipt = null;

            // Professional check: Ensure company settings are filled before proceeding
            var settings = _settingsService.CurrentSettings;
            if (string.IsNullOrWhiteSpace(settings.ShopName) ||
                string.IsNullOrWhiteSpace(settings.ShopAddress) ||
                string.IsNullOrWhiteSpace(settings.CompanyId) ||
                string.IsNullOrWhiteSpace(settings.VatId))
            {
                CheckoutFailed?.Invoke(this, "Chybí údaje o firmě. Prosím, doplňte je v sekci Nastavení před dokončením prodeje.");
                return;
            }

            var productsToUpdate = new List<Product>();
            var receiptItems = new List<Sklad_2.Models.ReceiptItem>();
            decimal totalAmountWithoutVat = 0;
            decimal totalVatAmount = 0;

            // 1. Preliminary check and data preparation
            foreach (var item in Receipt.Items)
            {
                var productInDb = await _dataService.GetProductAsync(item.Product.Ean);
                if (productInDb == null || productInDb.StockQuantity < item.Quantity)
                {
                    string errorMessage = $"Produkt '{item.Product.Name}' již není dostupný v požadovaném množství. Požadováno: {item.Quantity}, Skladem: {productInDb?.StockQuantity ?? 0}.";
                    CheckoutFailed?.Invoke(this, errorMessage);
                    return; // Abort checkout
                }

                // Prepare product for stock update
                productInDb.StockQuantity -= item.Quantity;
                productsToUpdate.Add(productInDb);

                // Prepare receipt item
                decimal itemPriceWithoutVat = item.TotalPrice / (1 + productInDb.VatRate);
                decimal itemVatAmount = item.TotalPrice - itemPriceWithoutVat;
                receiptItems.Add(new Sklad_2.Models.ReceiptItem
                {
                    ProductEan = item.Product.Ean,
                    ProductName = item.Product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.Product.SalePrice,
                    TotalPrice = item.TotalPrice,
                    VatRate = productInDb.VatRate,
                    PriceWithoutVat = itemPriceWithoutVat,
                    VatAmount = itemVatAmount
                });

                totalAmountWithoutVat += itemPriceWithoutVat;
                totalVatAmount += itemVatAmount;
            }

            // 2. Create Receipt object
            var newReceipt = new Sklad_2.Models.Receipt
            {
                SaleDate = DateTime.Now,
                TotalAmount = Receipt.GrandTotal,
                PaymentMethod = "Hotově",
                Items = receiptItems,
                ShopName = settings.ShopName,
                ShopAddress = settings.ShopAddress,
                CompanyId = settings.CompanyId,
                VatId = settings.VatId,
                IsVatPayer = settings.IsVatPayer,
                TotalAmountWithoutVat = totalAmountWithoutVat,
                TotalVatAmount = totalVatAmount
            };

            // 3. Call the atomic data service method
            var (success, serviceErrorMessage) = await _dataService.CompleteSaleAsync(newReceipt, productsToUpdate);

            if (success)
            {
                Receipt.Clear();
                ScannedProduct = null;
                LastCreatedReceipt = newReceipt;
                IsCheckoutSuccessful = true;
            }
            else
            {
                CheckoutFailed?.Invoke(this, serviceErrorMessage);
            }
        }
    }
}
