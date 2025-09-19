using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                        };
                    }
                }
                DecrementQuantityCommand.NotifyCanExecuteChanged();
            };
        }

        [RelayCommand]
        private async Task FindProductAsync(string eanCode)
        {
            if (string.IsNullOrWhiteSpace(eanCode)) return;
            
            ScannedProduct = null;
            var product = await _dataService.GetProductAsync(eanCode);
            if (product != null)
            {
                Receipt.AddProduct(product);
                ScannedProduct = product;
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

        [RelayCommand(CanExecute = nameof(CanManipulateItem))]
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
            LastCreatedReceipt = null;
            try
            {
                var settings = _settingsService.CurrentSettings;

                decimal totalAmountWithoutVat = 0;
                decimal totalVatAmount = 0;

                var receiptItems = new List<Sklad_2.Models.ReceiptItem>();

                // 1. Populate ReceiptItems, calculate VAT and deduct stock
                foreach (var item in Receipt.Items)
                {
                    var productInDb = await _dataService.GetProductAsync(item.Product.Ean);
                    if (productInDb != null)
                    {
                        if (productInDb.StockQuantity >= item.Quantity)
                        {
                            productInDb.StockQuantity -= item.Quantity;
                            await _dataService.UpdateProductAsync(productInDb);

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
                        else
                        {
                            // Logika pro nedostatečné zásoby
                            System.Diagnostics.Debug.WriteLine($"Nedostatečné zásoby pro produkt {item.Product.Name} (EAN: {item.Product.Ean}). Požadováno: {item.Quantity}, Skladem: {productInDb.StockQuantity}");
                        }
                    }
                }

                // 2. Create new Receipt object
                var newReceipt = new Sklad_2.Models.Receipt
                {
                    SaleDate = DateTime.Now,
                    TotalAmount = Receipt.GrandTotal,
                    PaymentMethod = "Hotově", // Placeholder
                    Items = receiptItems,

                    // Seller info
                    ShopName = settings.ShopName,
                    ShopAddress = settings.ShopAddress,
                    CompanyId = settings.CompanyId,
                    VatId = settings.VatId,
                    IsVatPayer = settings.IsVatPayer,

                    // VAT info
                    TotalAmountWithoutVat = totalAmountWithoutVat,
                    TotalVatAmount = totalVatAmount
                };

                // 3. Save the Receipt and its items
                await _dataService.SaveReceiptAsync(newReceipt);

                // 4. Clear current receipt
                Receipt.Clear();
                ScannedProduct = null;

                LastCreatedReceipt = newReceipt;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chyba během dokončení prodeje: {ex.Message}");
                // Zde by se v reálné aplikaci zobrazil uživateli dialog s chybou
            }
        }
    }
}
