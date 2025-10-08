using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class ProdejViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;
        private readonly ICashRegisterService _cashRegisterService;
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
        public string GrandTotalWithoutVatFormatted => $"Základ: {Receipt.GrandTotalWithoutVat:C}";
        public string GrandTotalVatAmountFormatted => $"DPH: {Receipt.GrandTotalVatAmount:C}";

        public ProdejViewModel(IDataService dataService, IReceiptService receiptService, ISettingsService settingsService, ICashRegisterService cashRegisterService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _cashRegisterService = cashRegisterService;
            Receipt = receiptService;

            // Listen for changes in the service to update UI
            if (Receipt is INotifyPropertyChanged notifiedReceipt)
            {
                notifiedReceipt.PropertyChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(GrandTotalFormatted));
                    OnPropertyChanged(nameof(GrandTotalWithoutVatFormatted));
                    OnPropertyChanged(nameof(GrandTotalVatAmountFormatted));
                };
            }
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
        private async Task CheckoutAsync(Dictionary<string, object> parameters)
        {
            IsCheckoutSuccessful = false;
            LastCreatedReceipt = null;

            parameters.TryGetValue("paymentMethod", out var paymentMethodObj);
            parameters.TryGetValue("receivedAmount", out var receivedAmountObj);
            parameters.TryGetValue("changeAmount", out var changeAmountObj);

            var paymentMethod = (paymentMethodObj is PaymentMethod) ? (PaymentMethod)paymentMethodObj : PaymentMethod.None;
            var receivedAmount = (receivedAmountObj is decimal) ? (decimal)receivedAmountObj : 0;
            var changeAmount = (changeAmountObj is decimal) ? (decimal)changeAmountObj : 0;

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
            var receiptItemsForDb = new List<Sklad_2.Models.ReceiptItem>();

            foreach (var item in Receipt.Items)
            {
                var productInDb = await _dataService.GetProductAsync(item.Product.Ean);
                if (productInDb == null || productInDb.StockQuantity < item.Quantity)
                {
                    string errorMessage = $"Produkt '{item.Product.Name}' již není dostupný v požadovaném množství. Požadováno: {item.Quantity}, Skladem: {productInDb?.StockQuantity ?? 0}.";
                    CheckoutFailed?.Invoke(this, errorMessage);
                    return;
                }

                productInDb.StockQuantity -= item.Quantity;
                productsToUpdate.Add(productInDb);

                receiptItemsForDb.Add(new Sklad_2.Models.ReceiptItem
                {
                    ProductEan = item.Product.Ean,
                    ProductName = item.Product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.Product.SalePrice,
                    TotalPrice = item.TotalPrice,
                    VatRate = item.Product.VatRate,
                    PriceWithoutVat = item.PriceWithoutVat,
                    VatAmount = item.VatAmount
                });
            }

            var newReceipt = new Sklad_2.Models.Receipt
            {
                SaleDate = DateTime.Now,
                TotalAmount = Receipt.GrandTotal,
                PaymentMethod = GetPaymentMethodString(paymentMethod),
                Items = new ObservableCollection<Sklad_2.Models.ReceiptItem>(receiptItemsForDb),
                ShopName = settings.ShopName,
                ShopAddress = settings.ShopAddress,
                CompanyId = settings.CompanyId,
                VatId = settings.VatId,
                IsVatPayer = settings.IsVatPayer,
                TotalAmountWithoutVat = Receipt.GrandTotalWithoutVat,
                TotalVatAmount = Receipt.GrandTotalVatAmount,
                ReceivedAmount = receivedAmount,
                ChangeAmount = changeAmount
            };

            var (success, serviceErrorMessage) = await _dataService.CompleteSaleAsync(newReceipt, productsToUpdate);

            if (success)
            {
                if (paymentMethod == PaymentMethod.Cash)
                {
                    await _cashRegisterService.RecordEntryAsync(EntryType.Sale, newReceipt.TotalAmount, $"Prodej účtenky #{newReceipt.ReceiptId}");
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send<Sklad_2.Messages.CashRegisterUpdatedMessage, string>(new Sklad_2.Messages.CashRegisterUpdatedMessage(), "CashRegisterUpdateToken");
                }
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

        private string GetPaymentMethodString(PaymentMethod paymentMethod)
        {
            return paymentMethod switch
            {
                PaymentMethod.Cash => "Hotově",
                PaymentMethod.Card => "Kartou",
                _ => "Neznámá",
            };
        }
    }
}
