using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.Services;
using Sklad_2.Views.Dialogs;
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
        private readonly IGiftCardService _giftCardService;
        private readonly IPrintService _printService;
        public IReceiptService Receipt { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProductFound))]
        [NotifyPropertyChangedFor(nameof(ScannedProductPriceFormatted))]
        private Product scannedProduct;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(IncrementQuantityCommand))]
        [NotifyCanExecuteChangedFor(nameof(DecrementQuantityCommand))]
        [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
        [NotifyCanExecuteChangedFor(nameof(ApplyManualDiscountCommand))]
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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGiftCardReady))]
        [NotifyPropertyChangedFor(nameof(GiftCardValueFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(GrandTotalFormatted))]
        [NotifyPropertyChangedFor(nameof(WillHavePartialUsage))]
        [NotifyPropertyChangedFor(nameof(ForfeitedAmount))]
        [NotifyPropertyChangedFor(nameof(ForfeitedAmountFormatted))]
        private GiftCard scannedGiftCard;

        [ObservableProperty]
        private string giftCardEanInput = string.Empty;

        public bool IsCheckoutSuccessful { get; private set; }
        public bool CanCancelLastReceipt => LastCreatedReceipt != null;
        public bool IsGiftCardReady => ScannedGiftCard != null && ScannedGiftCard.CanBeUsed;
        public string GiftCardValueFormatted => ScannedGiftCard != null ? ScannedGiftCard.ValueFormatted : string.Empty;

        /// <summary>
        /// Zjistí, zda dojde k částečnému využití poukazu (zbytek propadne)
        /// </summary>
        public bool WillHavePartialUsage => ScannedGiftCard != null && ScannedGiftCard.Value > Receipt.GrandTotal;

        /// <summary>
        /// Částka, která propadne při částečném využití
        /// </summary>
        public decimal ForfeitedAmount => WillHavePartialUsage ? ScannedGiftCard.Value - Receipt.GrandTotal : 0;

        public string ForfeitedAmountFormatted => ForfeitedAmount > 0 ? $"{ForfeitedAmount:C}" : string.Empty;

        public bool IsProductFound => ScannedProduct != null;
        private bool CanManipulateItem => SelectedReceiptItem != null;
        private bool CanApplyManualDiscount => SelectedReceiptItem != null && _settingsService.CurrentSettings.AllowManualDiscounts;

        public string ScannedProductPriceFormatted => ScannedProduct != null ? $"{ScannedProduct.SalePrice:C}" : string.Empty;

        /// <summary>
        /// Částka k úhradě po odečtení načteného poukazu
        /// </summary>
        public decimal AmountToPay => ScannedGiftCard != null
            ? Math.Max(0, Receipt.GrandTotal - ScannedGiftCard.Value)
            : Receipt.GrandTotal;

        public string GrandTotalFormatted => ScannedGiftCard != null
            ? $"Celkem: {Receipt.GrandTotal:C}\nK úhradě po poukazu: {AmountToPay:C}"
            : $"Celkem: {Receipt.GrandTotal:C}";
        public string GrandTotalWithoutVatFormatted => $"Základ: {Receipt.GrandTotalWithoutVat:C}";
        public string GrandTotalVatAmountFormatted => $"DPH: {Receipt.GrandTotalVatAmount:C}";

        public ProdejViewModel(IDataService dataService, IReceiptService receiptService, ISettingsService settingsService, ICashRegisterService cashRegisterService, IAuthService authService, IGiftCardService giftCardService, IPrintService printService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _cashRegisterService = cashRegisterService;
            _authService = authService;
            _giftCardService = giftCardService;
            _printService = printService;
            Receipt = receiptService;

            // Listen for changes in the service to update UI
            if (Receipt is INotifyPropertyChanged notifiedReceipt)
            {
                notifiedReceipt.PropertyChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(AmountToPay));
                    OnPropertyChanged(nameof(GrandTotalFormatted));
                    OnPropertyChanged(nameof(GrandTotalWithoutVatFormatted));
                    OnPropertyChanged(nameof(GrandTotalVatAmountFormatted));
                    OnPropertyChanged(nameof(WillHavePartialUsage));
                    OnPropertyChanged(nameof(ForfeitedAmount));
                    OnPropertyChanged(nameof(ForfeitedAmountFormatted));
                };
            }
            
            // Listen for settings changes to update manual discount availability
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<ProdejViewModel, Sklad_2.Messages.SettingsChangedMessage, string>(this, "SettingsUpdateToken", (r, m) =>
            {
                ApplyManualDiscountCommand.NotifyCanExecuteChanged();
            });
        }

        public event EventHandler<Product> ProductOutOfStock;
        public event EventHandler<string> CheckoutFailed;
        public event EventHandler<string> ReceiptCancelled;
        public event EventHandler<string> GiftCardValidationFailed;

        [RelayCommand]
        private async Task FindProductAsync(string eanCode)
        {
            if (string.IsNullOrWhiteSpace(eanCode)) return;

            // Clear last receipt when starting a new sale
            LastCreatedReceipt = null;

            ScannedProduct = null;

            // 1. Check if it's a gift card first
            var giftCard = await _giftCardService.GetGiftCardByEanAsync(eanCode);
            if (giftCard != null)
            {
                // Validate if gift card can be sold
                var (canSell, validationMessage) = await _giftCardService.CanSellGiftCardAsync(eanCode);
                if (!canSell)
                {
                    CheckoutFailed?.Invoke(this, validationMessage);
                    return;
                }

                // Create a pseudo-product for the gift card (appears as regular item in cart)
                var giftCardProduct = new Product
                {
                    Ean = giftCard.Ean,
                    Name = $"Dárkový poukaz {giftCard.ValueFormatted} (EAN: {giftCard.Ean})",
                    SalePrice = giftCard.Value,  // Positive price - customer pays for the voucher
                    VatRate = 0,  // No VAT on gift card sales (multi-purpose vouchers)
                    StockQuantity = 1,  // Only 1 per gift card
                    Category = "Dárkové poukazy"
                };

                ScannedProduct = giftCardProduct;
                Receipt.AddProduct(giftCardProduct);
                return;
            }

            // 2. Not a gift card, try regular product
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

        [RelayCommand(CanExecute = nameof(CanApplyManualDiscount))]
        private async Task ApplyManualDiscountAsync()
        {
            if (SelectedReceiptItem == null) return;

            var dialog = new ManualDiscountDialog();
            dialog.XamlRoot = (Application.Current as App)?.CurrentWindow?.Content?.XamlRoot;
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary && dialog.DiscountPercent > 0)
            {
                // Apply manual discount to selected item
                ApplyDiscountToCartItem(SelectedReceiptItem, dialog.DiscountPercent, dialog.DiscountReason);
            }
        }

        private void ApplyDiscountToCartItem(CartItem item, decimal discountPercent, string reason)
        {
            // Apply manual discount to the cart item
            item.ManualDiscountPercent = discountPercent;
            item.ManualDiscountReason = reason;
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
        private async Task LoadGiftCardForRedemptionAsync(string ean)
        {
            if (string.IsNullOrWhiteSpace(ean))
            {
                ScannedGiftCard = null;
                return;
            }

            // Validate gift card
            var (canUse, message, giftCard) = await _giftCardService.CanUseGiftCardAsync(ean);

            if (!canUse)
            {
                ScannedGiftCard = null;
                GiftCardValidationFailed?.Invoke(this, message);
                return;
            }

            // Gift card is valid and ready to use
            ScannedGiftCard = giftCard;
        }

        [RelayCommand]
        private void ClearGiftCard()
        {
            ScannedGiftCard = null;
            GiftCardEanInput = string.Empty;
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

            // Validate payment method (GiftCard is no longer a standalone payment method)
            if (paymentMethod == PaymentMethod.None || paymentMethod == PaymentMethod.GiftCard)
            {
                CheckoutFailed?.Invoke(this, "Musíte vybrat způsob platby (Hotově nebo Kartou).");
                return;
            }

            // Validate amounts for cash payment
            if (paymentMethod == PaymentMethod.Cash && (receivedAmount < 0 || changeAmount < 0))
            {
                CheckoutFailed?.Invoke(this, "Chyba: Neplatné částky při platbě hotovostí.");
                return;
            }

            var settings = _settingsService.CurrentSettings;

            // Base validation: ShopName, ShopAddress, CompanyId always required
            if (string.IsNullOrWhiteSpace(settings.ShopName) ||
                string.IsNullOrWhiteSpace(settings.ShopAddress) ||
                string.IsNullOrWhiteSpace(settings.CompanyId))
            {
                CheckoutFailed?.Invoke(this, "Chybí údaje o firmě. Prosím, doplňte je v sekci Nastavení před dokončením prodeje.");
                return;
            }

            // VatId only required if company is VAT payer
            if (settings.IsVatPayer && string.IsNullOrWhiteSpace(settings.VatId))
            {
                CheckoutFailed?.Invoke(this, "Chybí DIČ. Pro plátce DPH je DIČ povinné. Doplňte jej v sekci Nastavení.");
                return;
            }

            try
            {
                var productsToUpdate = new List<Product>();
                var receiptItemsForDb = new List<Sklad_2.Models.ReceiptItem>();
                var giftCardsToSell = new List<string>(); // EAN codes of gift cards being sold
                decimal giftCardSaleAmount = 0;

                foreach (var item in Receipt.Items)
                {
                    if (item == null || item.Product == null)
                    {
                        CheckoutFailed?.Invoke(this, "Chyba: Neplatná položka v košíku.");
                        return;
                    }

                    // Check if this is a gift card (negative price indicates gift card sale)
                    var isGiftCard = await _giftCardService.GetGiftCardByEanAsync(item.Product.Ean);
                    if (isGiftCard != null)
                    {
                        // This is a gift card sale
                        giftCardsToSell.Add(item.Product.Ean);
                        giftCardSaleAmount += Math.Abs(item.Product.SalePrice); // Absolute value (was negative in cart)

                        // Add to receipt items (with negative price)
                        receiptItemsForDb.Add(new Sklad_2.Models.ReceiptItem
                        {
                            ProductEan = item.Product.Ean,
                            ProductName = item.Product.Name,
                            Quantity = item.Quantity,
                            UnitPrice = item.Product.SalePrice,  // Negative
                            TotalPrice = item.TotalPrice,  // Negative
                            VatRate = 0,  // No VAT on gift card sales
                            PriceWithoutVat = item.TotalPrice,  // Same as total (no VAT)
                            VatAmount = 0,
                            OriginalUnitPrice = 0,  // Gift cards don't have original price
                            DiscountReason = string.Empty  // Gift cards don't have discounts
                        });
                        continue; // Skip regular product processing
                    }

                    // Regular product (not a gift card)
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
                        UnitPrice = item.UnitPrice, // Final price (with discount)
                        TotalPrice = item.TotalPrice,
                        VatRate = item.Product.VatRate,
                        PriceWithoutVat = item.PriceWithoutVat,
                        VatAmount = item.VatAmount,
                        DiscountPercent = item.HasDiscount ? item.DiscountPercent : null,
                        OriginalUnitPrice = item.OriginalUnitPrice,
                        DiscountReason = item.HasDiscount ? item.DiscountReason : string.Empty
                    });
                }

                // Get next receipt sequence number for current year
                int currentYear = DateTime.Now.Year;
                int nextSequence = await _dataService.GetNextReceiptSequenceAsync(currentYear);

                // Calculate gift card redemption amount (if gift card is loaded)
                decimal giftCardRedemptionAmount = 0;
                if (ScannedGiftCard != null)
                {
                    giftCardRedemptionAmount = Math.Min(ScannedGiftCard.Value, Receipt.GrandTotal);
                }

                // Build payment method string (include gift card if used)
                string paymentMethodString = GetPaymentMethodString(paymentMethod);
                if (ScannedGiftCard != null)
                {
                    paymentMethodString = $"{paymentMethodString} + Dárkový poukaz";
                }

                var newReceipt = new Sklad_2.Models.Receipt
                {
                    SaleDate = DateTime.Now,
                    ReceiptYear = currentYear,
                    ReceiptSequence = nextSequence,
                    TotalAmount = Receipt.GrandTotal,
                    PaymentMethod = paymentMethodString,
                    Items = new ObservableCollection<Sklad_2.Models.ReceiptItem>(receiptItemsForDb),
                    ShopName = settings.ShopName,
                    ShopAddress = settings.ShopAddress,
                    SellerName = _authService.CurrentUser?.DisplayName ?? "Neznámý",  // Store who performed the sale
                    CompanyId = settings.CompanyId,
                    VatId = settings.VatId ?? string.Empty,  // Empty string for non-VAT payers (NOT NULL constraint)
                    IsVatPayer = settings.IsVatPayer,
                    TotalAmountWithoutVat = Receipt.GrandTotalWithoutVat,
                    TotalVatAmount = Receipt.GrandTotalVatAmount,
                    ReceivedAmount = receivedAmount,
                    ChangeAmount = changeAmount,
                    // Gift card fields - sale
                    ContainsGiftCardSale = giftCardsToSell.Count > 0,
                    GiftCardSaleAmount = giftCardSaleAmount,
                    // Gift card fields - redemption
                    ContainsGiftCardRedemption = ScannedGiftCard != null,
                    GiftCardRedemptionAmount = giftCardRedemptionAmount,
                    RedeemedGiftCardEan = ScannedGiftCard?.Ean ?? string.Empty  // Empty string if no gift card (NOT NULL constraint)
                };

                var userName = _authService.CurrentUser?.DisplayName ?? "Neznámý";
                var result = await _dataService.CompleteSaleAsync(newReceipt, productsToUpdate, userName);
                var (success, serviceErrorMessage) = result;

                if (success)
                {
                    // Mark gift cards as sold (change state NotIssued → Issued)
                    foreach (var giftCardEan in giftCardsToSell)
                    {
                        var sellResult = await _giftCardService.SellGiftCardAsync(giftCardEan, newReceipt.ReceiptId, userName);
                        if (!sellResult.Success)
                        {
                            Debug.WriteLine($"Warning: Failed to mark gift card {giftCardEan} as sold: {sellResult.Message}");
                        }
                    }

                    // Mark gift card as used if one was loaded (change state Issued → Used)
                    if (ScannedGiftCard != null)
                    {
                        var useResult = await _giftCardService.UseGiftCardAsync(ScannedGiftCard.Ean, newReceipt.ReceiptId, userName);
                        if (!useResult.Success)
                        {
                            Debug.WriteLine($"Warning: Failed to mark gift card {ScannedGiftCard.Ean} as used: {useResult.Message}");
                        }
                    }

                    // Record cash register entry (only actual cash received goes to register)
                    if (paymentMethod == PaymentMethod.Cash)
                    {
                        // If gift card was used, only record the remaining amount paid in cash
                        decimal cashAmount = AmountToPay;  // This is already reduced by gift card
                        if (cashAmount > 0)
                        {
                            await _cashRegisterService.RecordEntryAsync(EntryType.Sale, cashAmount, $"Prodej účtenky #{newReceipt.ReceiptId}");
                            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send<Sklad_2.Messages.CashRegisterUpdatedMessage, string>(new Sklad_2.Messages.CashRegisterUpdatedMessage(), "CashRegisterUpdateToken");
                        }
                    }

                    // Print receipt
                    try
                    {
                        var printSuccess = await _printService.PrintReceiptAsync(newReceipt);
                        if (!printSuccess)
                        {
                            Debug.WriteLine("Warning: Failed to print receipt - printer may not be connected");
                        }
                    }
                    catch (Exception printEx)
                    {
                        Debug.WriteLine($"Warning: Exception during receipt printing: {printEx.Message}");
                    }

                    // Clear state
                    Receipt.Clear();
                    ScannedProduct = null;
                    ScannedGiftCard = null;
                    GiftCardEanInput = string.Empty;
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
                PaymentMethod.GiftCard => "Dárkovým poukazem",
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

                // 2. Return products to stock and cancel gift cards
                var productsToUpdate = new List<Product>();
                var giftCardsToCancel = new List<string>();

                foreach (var item in originalReceipt.Items)
                {
                    // Check if this is a gift card
                    var giftCard = await _giftCardService.GetGiftCardByEanAsync(item.ProductEan);
                    if (giftCard != null)
                    {
                        // This is a gift card - cancel the sale (Issued → NotIssued)
                        giftCardsToCancel.Add(item.ProductEan);
                        continue; // Skip regular product processing
                    }

                    // Regular product - return to stock
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

                // 4. Cancel gift card sales (restore states)
                foreach (var giftCardEan in giftCardsToCancel)
                {
                    var cancelResult = await _giftCardService.CancelSaleAsync(giftCardEan);
                    if (!cancelResult.Success)
                    {
                        Debug.WriteLine($"Warning: Failed to cancel gift card sale {giftCardEan}: {cancelResult.Message}");
                    }
                }

                // 4b. Cancel gift card redemption (if gift card was used in this receipt)
                if (originalReceipt.ContainsGiftCardRedemption)
                {
                    // Find the gift card that was used (by searching Used gift cards for this receipt)
                    var allUsedCards = await _giftCardService.GetGiftCardsByStatusAsync(GiftCardStatus.Used);
                    var usedCard = allUsedCards.FirstOrDefault(gc => gc.UsedOnReceiptId == receiptId);
                    if (usedCard != null)
                    {
                        var cancelRedemptionResult = await _giftCardService.CancelRedemptionAsync(usedCard.Ean);
                        if (!cancelRedemptionResult.Success)
                        {
                            Debug.WriteLine($"Warning: Failed to cancel gift card redemption {usedCard.Ean}: {cancelRedemptionResult.Message}");
                        }
                    }
                }

                // 5. Create STORNO receipt (new receipt with negative values)
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
                        VatAmount = -item.VatAmount,  // NEGATIVE
                        DiscountPercent = item.HasDiscount ? item.DiscountPercent : null,
                        OriginalUnitPrice = item.OriginalUnitPrice,
                        DiscountReason = item.HasDiscount ? item.DiscountReason : string.Empty
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
                    VatId = originalReceipt.VatId ?? string.Empty,  // NOT NULL constraint
                    IsVatPayer = originalReceipt.IsVatPayer,
                    TotalAmountWithoutVat = -originalReceipt.TotalAmountWithoutVat,  // NEGATIVE
                    TotalVatAmount = -originalReceipt.TotalVatAmount,  // NEGATIVE
                    ReceivedAmount = originalReceipt.ReceivedAmount,
                    ChangeAmount = originalReceipt.ChangeAmount,
                    IsStorno = true,  // Mark as STORNO
                    OriginalReceiptId = receiptId,  // Link to original
                    // Gift card fields (for storno, negate amounts)
                    ContainsGiftCardSale = originalReceipt.ContainsGiftCardSale,
                    GiftCardSaleAmount = -originalReceipt.GiftCardSaleAmount,  // NEGATIVE for storno
                    ContainsGiftCardRedemption = originalReceipt.ContainsGiftCardRedemption,
                    GiftCardRedemptionAmount = -originalReceipt.GiftCardRedemptionAmount,  // NEGATIVE for storno
                    RedeemedGiftCardEan = originalReceipt.RedeemedGiftCardEan ?? string.Empty  // NOT NULL constraint
                };

                // 5. Save storno receipt to DB
                await _dataService.SaveReceiptAsync(stornoReceipt);

                // 6. Remove from cash register (only for cash payments)
                // Check if payment method contains "Hotově" (can be "Hotově" or "Hotově + Dárkový poukaz")
                if (paymentMethod.Contains("Hotově"))
                {
                    // Calculate actual cash amount received (total minus gift card if used)
                    decimal cashAmount = originalReceipt.TotalAmount;
                    if (originalReceipt.ContainsGiftCardRedemption)
                    {
                        cashAmount -= originalReceipt.GiftCardRedemptionAmount;
                    }

                    // Only record if there was actual cash received
                    if (cashAmount > 0)
                    {
                        await _cashRegisterService.RecordEntryAsync(
                            EntryType.Return,
                            cashAmount,  // Return only actual cash amount
                            $"Storno účtenky #{receiptId} - vráceno zákazníkovi {cashAmount:C}");
                        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send<Sklad_2.Messages.CashRegisterUpdatedMessage, string>(
                            new Sklad_2.Messages.CashRegisterUpdatedMessage(), "CashRegisterUpdateToken");
                    }
                }

                // 7. Clear last receipt reference
                LastCreatedReceipt = null;

                // 8. Build notification message
                string cancelMessage = $"Účtenka #{receiptId} byla stornována.\n\n" +
                    $"Vytvořena storno účtenka #{stornoReceipt.ReceiptId} s negativními hodnotami.\n";

                // Add info about returned products
                if (productsToUpdate.Count > 0)
                {
                    cancelMessage += $"Produkty vráceny do skladu.\n";
                }

                // Add info about cash register change (only if cash was returned)
                if (paymentMethod.Contains("Hotově"))
                {
                    decimal actualCashReturned = originalReceipt.TotalAmount;
                    if (originalReceipt.ContainsGiftCardRedemption)
                    {
                        actualCashReturned -= originalReceipt.GiftCardRedemptionAmount;
                    }

                    if (actualCashReturned > 0)
                    {
                        cancelMessage += $"Částka {actualCashReturned:C} vrácena zákazníkovi a odečtena z pokladny.\n";
                    }
                    else
                    {
                        cancelMessage += $"Žádná hotovost nebyla přijata, pokladna nezměněna.\n";
                    }
                }

                cancelMessage += $"\nObě účtenky zůstávají v historii pro audit.";

                // 9. Notify UI
                ReceiptCancelled?.Invoke(this, cancelMessage);
            }
            catch (Exception ex)
            {
                CheckoutFailed?.Invoke(this, $"Chyba při rušení účtenky: {ex.Message}");
            }
        }
    }
}
