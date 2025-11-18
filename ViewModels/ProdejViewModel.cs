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
        private readonly IAuthService _authService;
        public IReceiptService Receipt { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProductFound))]
        [NotifyPropertyChangedFor(nameof(ScannedProductPriceFormatted))]
        private Product scannedProduct;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(IncrementQuantityCommand))]
        [NotifyCanExecuteChangedFor(nameof(DecrementQuantityCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
        private CartItem selectedReceiptItem;

        partial void OnSelectedReceiptItemChanged(CartItem oldValue, CartItem newValue)
        {
            // Unsubscribe from old item
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= OnSelectedItemPropertyChanged;
            }

            // Subscribe to new item
            if (newValue != null)
            {
                newValue.PropertyChanged += OnSelectedItemPropertyChanged;
            }
        }

        private void OnSelectedItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When Quantity changes on the selected item, re-evaluate CanExecute
            if (e.PropertyName == nameof(CartItem.Quantity))
            {
                IncrementQuantityCommand.NotifyCanExecuteChanged();
                DecrementQuantityCommand.NotifyCanExecuteChanged();
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCancelLastReceipt))]
        private Receipt lastCreatedReceipt;

        public bool IsCheckoutSuccessful { get; private set; }
        public bool CanCancelLastReceipt => LastCreatedReceipt != null;

        public bool IsProductFound => ScannedProduct != null;
        private bool CanManipulateItem => SelectedReceiptItem != null;

        public string ScannedProductPriceFormatted => ScannedProduct != null ? $"{ScannedProduct.SalePrice:C}" : string.Empty;
        public string GrandTotalFormatted => $"Celkem: {Receipt.GrandTotal:C}";
        public string GrandTotalWithoutVatFormatted => $"Základ: {Receipt.GrandTotalWithoutVat:C}";
        public string GrandTotalVatAmountFormatted => $"DPH: {Receipt.GrandTotalVatAmount:C}";

        public ProdejViewModel(IDataService dataService, IReceiptService receiptService, ISettingsService settingsService, ICashRegisterService cashRegisterService, IAuthService authService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _cashRegisterService = cashRegisterService;
            _authService = authService;
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
        public event EventHandler<string> ReceiptCancelled;

        [RelayCommand]
        private async Task FindProductAsync(string eanCode)
        {
            if (string.IsNullOrWhiteSpace(eanCode)) return;

            // Clear last receipt when starting a new sale
            LastCreatedReceipt = null;

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

            // Validate parameters
            if (parameters == null)
            {
                CheckoutFailed?.Invoke(this, "Chyba: Neplatné platební údaje.");
                return;
            }

            parameters.TryGetValue("paymentMethod", out var paymentMethodObj);
            parameters.TryGetValue("receivedAmount", out var receivedAmountObj);
            parameters.TryGetValue("changeAmount", out var changeAmountObj);

            var paymentMethod = (paymentMethodObj is PaymentMethod) ? (PaymentMethod)paymentMethodObj : PaymentMethod.None;
            var receivedAmount = (receivedAmountObj is decimal) ? (decimal)receivedAmountObj : 0;
            var changeAmount = (changeAmountObj is decimal) ? (decimal)changeAmountObj : 0;

            // Validate payment method
            if (paymentMethod == PaymentMethod.None)
            {
                CheckoutFailed?.Invoke(this, "Musíte vybrat způsob platby.");
                return;
            }

            // Validate amounts for cash payment
            if (paymentMethod == PaymentMethod.Cash && (receivedAmount < 0 || changeAmount < 0))
            {
                CheckoutFailed?.Invoke(this, "Chyba: Neplatné částky při platbě hotovostí.");
                return;
            }

            var settings = _settingsService.CurrentSettings;
            if (string.IsNullOrWhiteSpace(settings.ShopName) ||
                string.IsNullOrWhiteSpace(settings.ShopAddress) ||
                string.IsNullOrWhiteSpace(settings.CompanyId) ||
                string.IsNullOrWhiteSpace(settings.VatId))
            {
                CheckoutFailed?.Invoke(this, "Chybí údaje o firmě. Prosím, doplňte je v sekci Nastavení před dokončením prodeje.");
                return;
            }

            try
            {
                var productsToUpdate = new List<Product>();
                var receiptItemsForDb = new List<Sklad_2.Models.ReceiptItem>();

                foreach (var item in Receipt.Items)
                {
                    if (item == null || item.Product == null)
                    {
                        CheckoutFailed?.Invoke(this, "Chyba: Neplatná položka v košíku.");
                        return;
                    }

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

                // Get next receipt sequence number for current year
                int currentYear = DateTime.Now.Year;
                int nextSequence = await _dataService.GetNextReceiptSequenceAsync(currentYear);

                var newReceipt = new Sklad_2.Models.Receipt
                {
                    SaleDate = DateTime.Now,
                    ReceiptYear = currentYear,
                    ReceiptSequence = nextSequence,
                    TotalAmount = Receipt.GrandTotal,
                    PaymentMethod = GetPaymentMethodString(paymentMethod),
                    Items = new ObservableCollection<Sklad_2.Models.ReceiptItem>(receiptItemsForDb),
                    ShopName = settings.ShopName,
                    ShopAddress = settings.ShopAddress,
                    SellerName = _authService.CurrentUser?.DisplayName ?? "Neznámý",  // Store who performed the sale
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
            catch (Exception ex)
            {
                CheckoutFailed?.Invoke(this, $"Neočekávaná chyba při dokončování prodeje: {ex.Message}");
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

        [RelayCommand]
        private async Task CancelLastReceiptAsync()
        {
            if (LastCreatedReceipt == null) return;

            try
            {
                var receiptId = LastCreatedReceipt.ReceiptId;
                var totalAmount = LastCreatedReceipt.TotalAmount;
                var paymentMethod = LastCreatedReceipt.PaymentMethod;

                // 1. Get full receipt from DB with items
                var originalReceipt = await _dataService.GetReceiptByIdAsync(receiptId);
                if (originalReceipt == null || originalReceipt.Items == null)
                {
                    CheckoutFailed?.Invoke(this, "Chyba: Účtenka nebyla nalezena v databázi.");
                    return;
                }

                // 2. Return products to stock
                var productsToUpdate = new List<Product>();
                foreach (var item in originalReceipt.Items)
                {
                    var product = await _dataService.GetProductAsync(item.ProductEan);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        productsToUpdate.Add(product);
                    }
                }

                // 3. Update products in DB
                foreach (var product in productsToUpdate)
                {
                    await _dataService.UpdateProductAsync(product);
                }

                // 4. Create STORNO receipt (new receipt with negative values)
                var stornoItems = new ObservableCollection<Sklad_2.Models.ReceiptItem>();
                foreach (var item in originalReceipt.Items)
                {
                    stornoItems.Add(new Sklad_2.Models.ReceiptItem
                    {
                        ProductEan = item.ProductEan,
                        ProductName = item.ProductName,
                        Quantity = -item.Quantity,  // NEGATIVE quantity
                        UnitPrice = item.UnitPrice,
                        TotalPrice = -item.TotalPrice,  // NEGATIVE total
                        VatRate = item.VatRate,
                        PriceWithoutVat = -item.PriceWithoutVat,  // NEGATIVE
                        VatAmount = -item.VatAmount  // NEGATIVE
                    });
                }

                // Get next receipt sequence number for storno receipt
                int currentYear = DateTime.Now.Year;
                int nextSequence = await _dataService.GetNextReceiptSequenceAsync(currentYear);

                var stornoReceipt = new Sklad_2.Models.Receipt
                {
                    SaleDate = DateTime.Now,
                    ReceiptYear = currentYear,
                    ReceiptSequence = nextSequence,
                    TotalAmount = -originalReceipt.TotalAmount,  // NEGATIVE
                    PaymentMethod = originalReceipt.PaymentMethod,
                    Items = stornoItems,
                    ShopName = originalReceipt.ShopName,
                    ShopAddress = originalReceipt.ShopAddress,
                    SellerName = _authService.CurrentUser?.DisplayName ?? "Neznámý",  // Who performed the cancellation
                    CompanyId = originalReceipt.CompanyId,
                    VatId = originalReceipt.VatId,
                    IsVatPayer = originalReceipt.IsVatPayer,
                    TotalAmountWithoutVat = -originalReceipt.TotalAmountWithoutVat,  // NEGATIVE
                    TotalVatAmount = -originalReceipt.TotalVatAmount,  // NEGATIVE
                    ReceivedAmount = originalReceipt.ReceivedAmount,
                    ChangeAmount = originalReceipt.ChangeAmount,
                    IsStorno = true,  // Mark as STORNO
                    OriginalReceiptId = receiptId  // Link to original
                };

                // 5. Save storno receipt to DB
                await _dataService.SaveReceiptAsync(stornoReceipt);

                // 6. Remove from cash register (only for cash payments)
                if (paymentMethod == "Hotově")
                {
                    await _cashRegisterService.RecordEntryAsync(
                        EntryType.Return,
                        totalAmount,
                        $"Storno účtenky #{receiptId}");
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send<Sklad_2.Messages.CashRegisterUpdatedMessage, string>(
                        new Sklad_2.Messages.CashRegisterUpdatedMessage(), "CashRegisterUpdateToken");
                }

                // 7. Clear last receipt reference
                LastCreatedReceipt = null;

                // 8. Notify UI
                ReceiptCancelled?.Invoke(this, $"Účtenka #{receiptId} byla stornována.\n\n" +
                    $"Vytvořena storno účtenka #{stornoReceipt.ReceiptId} s negativními hodnotami.\n" +
                    $"Produkty vráceny do skladu, částka {totalAmount:C} odečtena z pokladny.\n\n" +
                    $"Obě účtenky zůstávají v historii pro audit.");
            }
            catch (Exception ex)
            {
                CheckoutFailed?.Invoke(this, $"Chyba při rušení účtenky: {ex.Message}");
            }
        }
    }
}
