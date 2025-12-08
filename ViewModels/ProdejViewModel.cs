using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Sklad_2.Data;
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
        private readonly IAuthService _authService;
        private readonly IGiftCardService _giftCardService;
        private readonly IPrintService _printService;
        private readonly IProductImageService _imageService;
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
        public IReceiptService Receipt { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProductFound))]
        [NotifyPropertyChangedFor(nameof(ScannedProductPriceFormatted))]
        [NotifyPropertyChangedFor(nameof(ScannedProductImage))]
        private Product scannedProduct;

        /// <summary>
        /// Gets the image for the scanned product.
        /// </summary>
        public BitmapImage ScannedProductImage => ScannedProduct != null && ScannedProduct.HasImage
            ? _imageService?.GetImage(ScannedProduct.Ean)
            : null;

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

        // Více uplatněných dárkových poukazů
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyGiftCardReady))]
        [NotifyPropertyChangedFor(nameof(HasAnyDiscount))]
        [NotifyPropertyChangedFor(nameof(TotalGiftCardValue))]
        [NotifyPropertyChangedFor(nameof(TotalGiftCardValueFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(AmountToPayFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPayRounded))]
        [NotifyPropertyChangedFor(nameof(RoundingDifference))]
        [NotifyPropertyChangedFor(nameof(HasRounding))]
        [NotifyPropertyChangedFor(nameof(AmountToPayRoundedFormatted))]
        [NotifyPropertyChangedFor(nameof(RoundingDifferenceFormatted))]
        [NotifyPropertyChangedFor(nameof(GrandTotalFormatted))]
        [NotifyPropertyChangedFor(nameof(WillHavePartialUsage))]
        [NotifyPropertyChangedFor(nameof(ForfeitedAmount))]
        [NotifyPropertyChangedFor(nameof(ForfeitedAmountFormatted))]
        private ObservableCollection<GiftCard> redeemedGiftCards = new();

        [ObservableProperty]
        private string giftCardEanInput = string.Empty;

        // Věrnostní program
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoyaltyCustomerLoaded))]
        [NotifyPropertyChangedFor(nameof(HasAnyDiscount))]
        [NotifyPropertyChangedFor(nameof(LoyaltyDiscountAmount))]
        [NotifyPropertyChangedFor(nameof(LoyaltyDiscountFormatted))]
        [NotifyPropertyChangedFor(nameof(LoyaltyDiscountAmountFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(AmountToPayFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPayRounded))]
        [NotifyPropertyChangedFor(nameof(RoundingDifference))]
        [NotifyPropertyChangedFor(nameof(HasRounding))]
        [NotifyPropertyChangedFor(nameof(AmountToPayRoundedFormatted))]
        [NotifyPropertyChangedFor(nameof(RoundingDifferenceFormatted))]
        [NotifyPropertyChangedFor(nameof(GrandTotalFormatted))]
        private LoyaltyCustomer selectedLoyaltyCustomer;

        [ObservableProperty]
        private string loyaltySearchInput = string.Empty;

        [ObservableProperty]
        private ObservableCollection<LoyaltyCustomer> loyaltySearchResults = new();

        public bool IsLoyaltyCustomerLoaded => SelectedLoyaltyCustomer != null;
        public decimal LoyaltyDiscountAmount => SelectedLoyaltyCustomer != null && SelectedLoyaltyCustomer.DiscountPercent > 0
            ? Math.Round(GetDiscountableAmount() * (SelectedLoyaltyCustomer.DiscountPercent / 100m), 2)
            : 0;
        public string LoyaltyDiscountFormatted => LoyaltyDiscountAmount > 0
            ? $"-{LoyaltyDiscountAmount:C} ({SelectedLoyaltyCustomer.DiscountPercent:N0}%)"
            : string.Empty;

        /// <summary>
        /// Formátovaná částka věrnostní slevy (jen hodnota)
        /// </summary>
        public string LoyaltyDiscountAmountFormatted => LoyaltyDiscountAmount > 0
            ? $"-{LoyaltyDiscountAmount:C}"
            : string.Empty;

        public bool IsCheckoutSuccessful { get; private set; }
        public bool CanCancelLastReceipt => LastCreatedReceipt != null;

        // Computed properties pro více poukazů
        public bool IsAnyGiftCardReady => RedeemedGiftCards != null && RedeemedGiftCards.Any();
        public decimal TotalGiftCardValue => RedeemedGiftCards?.Sum(gc => gc.Value) ?? 0;
        public string TotalGiftCardValueFormatted => TotalGiftCardValue > 0 ? $"{TotalGiftCardValue:C}" : string.Empty;

        /// <summary>
        /// Zjistí, zda dojde k částečnému využití poukazů (zbytek propadne)
        /// </summary>
        public bool WillHavePartialUsage => TotalGiftCardValue > Receipt.GrandTotal;

        /// <summary>
        /// Částka, která propadne při částečném využití
        /// </summary>
        public decimal ForfeitedAmount => WillHavePartialUsage ? TotalGiftCardValue - Receipt.GrandTotal : 0;

        public string ForfeitedAmountFormatted => ForfeitedAmount > 0 ? $"{ForfeitedAmount:C}" : string.Empty;

        public bool IsProductFound => ScannedProduct != null;
        private bool CanManipulateItem => SelectedReceiptItem != null;
        private bool CanApplyManualDiscount => SelectedReceiptItem != null && _settingsService.CurrentSettings.AllowManualDiscounts;

        public string ScannedProductPriceFormatted => ScannedProduct != null ? $"{ScannedProduct.SalePrice:C}" : string.Empty;

        /// <summary>
        /// Částka k úhradě po odečtení věrnostní slevy a poukazů (PŘESNÁ hodnota s haléři)
        /// </summary>
        public decimal AmountToPay
        {
            get
            {
                var total = Receipt.GrandTotal;

                // Odečíst věrnostní slevu
                total -= LoyaltyDiscountAmount;

                // Odečíst poukazy
                total = Math.Max(0, total - TotalGiftCardValue);

                return Math.Max(0, total);
            }
        }

        /// <summary>
        /// Zaokrouhlená částka k úhradě na celé koruny (pro zobrazení zákazníkovi)
        /// </summary>
        public decimal AmountToPayRounded => Math.Round(AmountToPay, 0, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Rozdíl zaokrouhlení
        /// </summary>
        public decimal RoundingDifference => AmountToPayRounded - AmountToPay;

        /// <summary>
        /// True pokud existuje rozdíl zaokrouhlení (není 0)
        /// </summary>
        public bool HasRounding => RoundingDifference != 0;

        /// <summary>
        /// Formátovaná přesná částka k úhradě (s haléři)
        /// </summary>
        public string AmountToPayFormatted => $"{AmountToPay:C}";

        /// <summary>
        /// Formátovaná zaokrouhlená částka k úhradě
        /// </summary>
        public string AmountToPayRoundedFormatted => $"{AmountToPayRounded:N0} Kč";

        /// <summary>
        /// Formátovaný rozdíl zaokrouhlení
        /// </summary>
        public string RoundingDifferenceFormatted => RoundingDifference >= 0
            ? $"+{RoundingDifference:F2} Kč"
            : $"{RoundingDifference:F2} Kč";

        /// <summary>
        /// True pokud je aplikována jakákoliv sleva (věrnostní nebo poukaz)
        /// </summary>
        public bool HasAnyDiscount => IsLoyaltyCustomerLoaded || IsAnyGiftCardReady;

        public string GrandTotalFormatted
        {
            get
            {
                var lines = new List<string> { $"Celkem: {Receipt.GrandTotal:C}" };

                if (LoyaltyDiscountAmount > 0)
                {
                    lines.Add($"Věrnostní sleva: {LoyaltyDiscountFormatted}");
                }

                if (IsAnyGiftCardReady)
                {
                    if (RedeemedGiftCards.Count == 1)
                    {
                        lines.Add($"Poukaz: -{TotalGiftCardValue:C}");
                    }
                    else
                    {
                        lines.Add($"Poukazy ({RedeemedGiftCards.Count}×): -{TotalGiftCardValue:C}");
                    }
                }

                if (LoyaltyDiscountAmount > 0 || IsAnyGiftCardReady)
                {
                    lines.Add($"K úhradě: {AmountToPay:C}");
                }

                return string.Join("\n", lines);
            }
        }

        /// <summary>
        /// Částka ze které se počítá věrnostní sleva (bez prodeje poukazů)
        /// </summary>
        private decimal GetDiscountableAmount()
        {
            // Věrnostní sleva se nepočítá z prodeje dárkových poukazů, pouze z běžných položek
            // Dárkové poukazy mají Category = "Dárkové poukazy" (nastaveno při přidání do košíku)
            return Receipt.Items
                .Where(item => item.Product != null && item.Product.Category != "Dárkové poukazy")
                .Sum(item => item.TotalPrice);
        }
        public string GrandTotalWithoutVatFormatted => $"Základ: {Receipt.GrandTotalWithoutVat:C}";
        public string GrandTotalVatAmountFormatted => $"DPH: {Receipt.GrandTotalVatAmount:C}";

        public ProdejViewModel(IDataService dataService, IReceiptService receiptService, ISettingsService settingsService, IAuthService authService, IGiftCardService giftCardService, IPrintService printService, IProductImageService imageService, IDbContextFactory<DatabaseContext> contextFactory)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _authService = authService;
            _giftCardService = giftCardService;
            _printService = printService;
            _imageService = imageService;
            _contextFactory = contextFactory;
            Receipt = receiptService;

            // Listen for changes in the service to update UI
            if (Receipt is INotifyPropertyChanged notifiedReceipt)
            {
                notifiedReceipt.PropertyChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(AmountToPay));
                    OnPropertyChanged(nameof(AmountToPayFormatted));
                    OnPropertyChanged(nameof(AmountToPayRounded));
                    OnPropertyChanged(nameof(RoundingDifference));
                    OnPropertyChanged(nameof(HasRounding));
                    OnPropertyChanged(nameof(AmountToPayRoundedFormatted));
                    OnPropertyChanged(nameof(RoundingDifferenceFormatted));
                    OnPropertyChanged(nameof(GrandTotalFormatted));
                    OnPropertyChanged(nameof(GrandTotalWithoutVatFormatted));
                    OnPropertyChanged(nameof(GrandTotalVatAmountFormatted));
                    OnPropertyChanged(nameof(WillHavePartialUsage));
                    OnPropertyChanged(nameof(ForfeitedAmount));
                    OnPropertyChanged(nameof(ForfeitedAmountFormatted));
                };
            }

            // Listen for changes in RedeemedGiftCards collection to update computed properties
            RedeemedGiftCards.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsAnyGiftCardReady));
                OnPropertyChanged(nameof(TotalGiftCardValue));
                OnPropertyChanged(nameof(TotalGiftCardValueFormatted));
                OnPropertyChanged(nameof(AmountToPay));
                OnPropertyChanged(nameof(AmountToPayFormatted));
                OnPropertyChanged(nameof(AmountToPayRounded));
                OnPropertyChanged(nameof(RoundingDifference));
                OnPropertyChanged(nameof(HasRounding));
                OnPropertyChanged(nameof(AmountToPayRoundedFormatted));
                OnPropertyChanged(nameof(RoundingDifferenceFormatted));
                OnPropertyChanged(nameof(GrandTotalFormatted));
                OnPropertyChanged(nameof(WillHavePartialUsage));
                OnPropertyChanged(nameof(ForfeitedAmount));
                OnPropertyChanged(nameof(ForfeitedAmountFormatted));
            };

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
                // CRITICAL: Check if this gift card is already loaded for payment (prevent sell+use in same receipt)
                if (RedeemedGiftCards.Any(gc => gc.Ean == eanCode))
                {
                    CheckoutFailed?.Invoke(this, "Tento poukaz je již načten pro platbu. Nelze jej současně prodávat a používat k úhradě.");
                    return;
                }

                // CRITICAL: Check if this gift card is already in cart (each gift card has unique EAN, can only sell 1)
                var existingGiftCard = Receipt.Items.FirstOrDefault(item =>
                    item.Product?.Category == "Dárkové poukazy" && item.Product?.Ean == eanCode);
                if (existingGiftCard != null)
                {
                    CheckoutFailed?.Invoke(this, "Tento poukaz je již v košíku. Každý poukaz má unikátní EAN a lze prodat pouze 1 kus.");
                    return;
                }

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
            RedeemedGiftCards.Clear();
            SelectedLoyaltyCustomer = null;
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
                return;
            }

            // CRITICAL: Check if this gift card is already in cart for sale (prevent sell+use in same receipt)
            var giftCardInCart = Receipt.Items.FirstOrDefault(item =>
                item.Product?.Category == "Dárkové poukazy" && item.Product?.Ean == ean);
            if (giftCardInCart != null)
            {
                GiftCardValidationFailed?.Invoke(this, "Tento poukaz je již v košíku pro prodej. Nelze jej současně prodávat a používat k úhradě.");
                return;
            }

            // Check if this gift card is already loaded for redemption
            if (RedeemedGiftCards.Any(gc => gc.Ean == ean))
            {
                GiftCardValidationFailed?.Invoke(this, "Tento poukaz je již načten pro uplatnění.");
                return;
            }

            // Validate gift card
            var (canUse, message, giftCard) = await _giftCardService.CanUseGiftCardAsync(ean);

            if (!canUse)
            {
                GiftCardValidationFailed?.Invoke(this, message);
                return;
            }

            // Add gift card to redemption list
            RedeemedGiftCards.Add(giftCard);
            GiftCardEanInput = string.Empty; // Clear input after successful add
        }

        [RelayCommand]
        private void RemoveGiftCard(string ean)
        {
            var cardToRemove = RedeemedGiftCards.FirstOrDefault(gc => gc.Ean == ean);
            if (cardToRemove != null)
            {
                RedeemedGiftCards.Remove(cardToRemove);
            }
        }

        [RelayCommand]
        private void ClearAllGiftCards()
        {
            RedeemedGiftCards.Clear();
            GiftCardEanInput = string.Empty;
        }

        // Věrnostní program - vyhledávání
        [RelayCommand]
        private async Task SearchLoyaltyCustomersAsync(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
                {
                    LoyaltySearchResults.Clear();
                    return;
                }

                using var context = await _contextFactory.CreateDbContextAsync();
                var searchLower = searchText.ToLower();

                var results = await context.LoyaltyCustomers
                    .AsNoTracking()
                    .Where(c =>
                        c.FirstName.ToLower().Contains(searchLower) ||
                        c.LastName.ToLower().Contains(searchLower) ||
                        c.Email.ToLower().Contains(searchLower) ||
                        (c.PhoneNumber != null && c.PhoneNumber.Contains(searchText)) ||
                        (c.CardEan != null && c.CardEan.Contains(searchText)))
                    .Take(10)
                    .ToListAsync();

                LoyaltySearchResults.Clear();
                foreach (var customer in results)
                {
                    LoyaltySearchResults.Add(customer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProdejViewModel: Error searching loyalty customers: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task LoadLoyaltyCustomerByEanAsync(string ean)
        {
            if (string.IsNullOrWhiteSpace(ean))
            {
                SelectedLoyaltyCustomer = null;
                return;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var customer = await context.LoyaltyCustomers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CardEan == ean);

                SelectedLoyaltyCustomer = customer;

                if (customer == null)
                {
                    LoyaltyValidationFailed?.Invoke(this, "Kartička nebyla nalezena.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProdejViewModel: Error loading loyalty customer: {ex.Message}");
                LoyaltyValidationFailed?.Invoke(this, $"Chyba: {ex.Message}");
            }
        }

        public void SelectLoyaltyCustomer(LoyaltyCustomer customer)
        {
            SelectedLoyaltyCustomer = customer;
            LoyaltySearchResults.Clear();
            LoyaltySearchInput = string.Empty;
        }

        [RelayCommand]
        private void ClearLoyaltyCustomer()
        {
            SelectedLoyaltyCustomer = null;
            LoyaltySearchInput = string.Empty;
            LoyaltySearchResults.Clear();
        }

        public event EventHandler<string> LoyaltyValidationFailed;

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

                    // CRITICAL: Clear navigation properties to prevent EF tracking conflicts
                    // If multiple products have the same Brand/Category, EF would try to track them multiple times
                    productInDb.Brand = null;
                    productInDb.ProductCategory = null;

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

                // Calculate loyalty discount (if customer is loaded)
                decimal loyaltyDiscountAmount = LoyaltyDiscountAmount;
                string loyaltyCustomerMaskedContact = SelectedLoyaltyCustomer?.MaskedContact ?? string.Empty;
                decimal loyaltyDiscountPercent = SelectedLoyaltyCustomer?.DiscountPercent ?? 0;

                // Calculate gift card redemption amount (if gift cards are loaded)
                // Gift cards apply AFTER loyalty discount
                decimal giftCardRedemptionAmount = 0;
                decimal totalAfterLoyaltyDiscount = Receipt.GrandTotal - loyaltyDiscountAmount;
                if (RedeemedGiftCards.Any())
                {
                    decimal totalGiftCardValue = RedeemedGiftCards.Sum(gc => gc.Value);
                    giftCardRedemptionAmount = Math.Min(totalGiftCardValue, totalAfterLoyaltyDiscount);
                }

                // Build payment method string (include gift cards if used)
                string paymentMethodString = GetPaymentMethodString(paymentMethod);
                if (RedeemedGiftCards.Any())
                {
                    if (RedeemedGiftCards.Count == 1)
                    {
                        paymentMethodString = $"{paymentMethodString} + Dárkový poukaz";
                    }
                    else
                    {
                        paymentMethodString = $"{paymentMethodString} + Dárkové poukazy ({RedeemedGiftCards.Count}×)";
                    }
                }

                // Calculate actual payment amounts (cash vs card)
                // Amount to pay = Total - Loyalty discount - Gift card redemption
                decimal amountToPay = totalAfterLoyaltyDiscount - giftCardRedemptionAmount;

                // Zaokrouhlení na celé koruny (FÚ compliance - hotovostní platby)
                decimal roundedAmount = Math.Round(amountToPay, 0, MidpointRounding.AwayFromZero);

                decimal cashAmount = 0;
                decimal cardAmount = 0;

                // Determine payment breakdown based on payment method
                if (paymentMethod == PaymentMethod.Card || paymentMethodString.Contains("Karta"))
                {
                    cardAmount = roundedAmount;  // Zaokrouhlená částka
                    cashAmount = 0;
                }
                else // Hotově
                {
                    cashAmount = roundedAmount;  // Zaokrouhlená částka
                    cardAmount = 0;
                }

                var newReceipt = new Sklad_2.Models.Receipt
                {
                    SaleDate = DateTime.Now,
                    ReceiptYear = currentYear,
                    ReceiptSequence = nextSequence,
                    TotalAmount = Receipt.GrandTotal,  // Original total before loyalty discount
                    PaymentMethod = paymentMethodString,
                    CashAmount = cashAmount,
                    CardAmount = cardAmount,
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
                    ContainsGiftCardRedemption = RedeemedGiftCards.Any(),
                    GiftCardRedemptionAmount = giftCardRedemptionAmount,
                    // Loyalty program fields
                    HasLoyaltyDiscount = loyaltyDiscountAmount > 0,
                    LoyaltyCustomerId = SelectedLoyaltyCustomer?.Id,  // Store ID for storno TotalPurchases update
                    LoyaltyCustomerContact = loyaltyCustomerMaskedContact,
                    LoyaltyDiscountPercent = loyaltyDiscountPercent,
                    LoyaltyDiscountAmount = loyaltyDiscountAmount
                };

                var userName = _authService.CurrentUser?.DisplayName ?? "Neznámý";

                Debug.WriteLine($"[CHECKOUT] Calling CompleteSaleAsync...");
                Debug.WriteLine($"[CHECKOUT] Receipt: Year={newReceipt.ReceiptYear}, Seq={newReceipt.ReceiptSequence}, Total={newReceipt.TotalAmount}");
                Debug.WriteLine($"[CHECKOUT] ShopName={newReceipt.ShopName}, CompanyId={newReceipt.CompanyId}, VatId={newReceipt.VatId}");
                Debug.WriteLine($"[CHECKOUT] Products to update: {productsToUpdate.Count}");

                var result = await _dataService.CompleteSaleAsync(newReceipt, productsToUpdate, userName);
                var (success, serviceErrorMessage) = result;

                Debug.WriteLine($"[CHECKOUT] CompleteSaleAsync result: success={success}, error={serviceErrorMessage}");

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

                    // Mark gift cards as used and save to ReceiptGiftCardRedemptions table
                    if (RedeemedGiftCards.Any())
                    {
                        using var context = await _contextFactory.CreateDbContextAsync();
                        decimal remainingToRedeem = giftCardRedemptionAmount;

                        foreach (var giftCard in RedeemedGiftCards)
                        {
                            // Mark gift card as used (change state Issued → Used)
                            var useResult = await _giftCardService.UseGiftCardAsync(giftCard.Ean, newReceipt.ReceiptId, userName);
                            if (!useResult.Success)
                            {
                                Debug.WriteLine($"Warning: Failed to mark gift card {giftCard.Ean} as used: {useResult.Message}");
                            }

                            // Calculate how much of this card was actually redeemed
                            decimal redeemedFromThisCard = Math.Min(giftCard.Value, remainingToRedeem);
                            remainingToRedeem -= redeemedFromThisCard;

                            // Save to ReceiptGiftCardRedemptions table
                            var redemption = new ReceiptGiftCardRedemption
                            {
                                ReceiptId = newReceipt.ReceiptId,
                                GiftCardEan = giftCard.Ean,
                                RedeemedAmount = redeemedFromThisCard
                            };
                            context.ReceiptGiftCardRedemptions.Add(redemption);
                        }

                        await context.SaveChangesAsync();
                        Debug.WriteLine($"ProdejViewModel: Saved {RedeemedGiftCards.Count} gift card redemptions to database");
                    }

                    // Update loyalty customer's TotalPurchases
                    if (SelectedLoyaltyCustomer != null)
                    {
                        try
                        {
                            using var context = await _contextFactory.CreateDbContextAsync();
                            var customer = await context.LoyaltyCustomers.FirstOrDefaultAsync(c => c.Id == SelectedLoyaltyCustomer.Id);
                            if (customer != null)
                            {
                                // DRY: Add only what customer actually paid (excludes loyalty discount AND gift card redemption)
                                // Gift card redemption is not counted because that value was already counted when the gift card was purchased
                                decimal purchaseAmount = Receipt.GrandTotal - loyaltyDiscountAmount - giftCardRedemptionAmount;
                                customer.TotalPurchases += purchaseAmount;
                                await context.SaveChangesAsync();
                                Debug.WriteLine($"ProdejViewModel: Updated TotalPurchases for {customer.Email}: +{purchaseAmount:C} = {customer.TotalPurchases:C}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"ProdejViewModel: Failed to update TotalPurchases: {ex.Message}");
                            // Don't fail the sale, just log the error
                        }
                    }

                    // Print receipt (load RedeemedGiftCards for printing)
                    try
                    {
                        // Load RedeemedGiftCards navigation property for print service
                        using var printContext = await _contextFactory.CreateDbContextAsync();
                        var receiptForPrint = await printContext.Receipts
                            .Include(r => r.RedeemedGiftCards)
                            .FirstOrDefaultAsync(r => r.ReceiptId == newReceipt.ReceiptId);

                        if (receiptForPrint != null)
                        {
                            // Copy navigation property to newReceipt for printing
                            newReceipt.RedeemedGiftCards = receiptForPrint.RedeemedGiftCards;
                        }

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
                    RedeemedGiftCards.Clear();
                    GiftCardEanInput = string.Empty;
                    SelectedLoyaltyCustomer = null;
                    LoyaltySearchInput = string.Empty;
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

                        // CRITICAL: Clear navigation properties to prevent EF tracking conflicts
                        product.Brand = null;
                        product.ProductCategory = null;

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

                // 4c. Revert loyalty customer TotalPurchases (if loyalty discount was applied)
                if (originalReceipt.LoyaltyCustomerId.HasValue)
                {
                    try
                    {
                        using var context = await _contextFactory.CreateDbContextAsync();
                        var customer = await context.LoyaltyCustomers.FirstOrDefaultAsync(c => c.Id == originalReceipt.LoyaltyCustomerId.Value);
                        if (customer != null)
                        {
                            // DRY: Use AmountToPay - what customer actually paid (excludes loyalty discount AND gift card redemption)
                            decimal purchaseAmount = originalReceipt.AmountToPay;
                            customer.TotalPurchases -= purchaseAmount;

                            // Ensure TotalPurchases doesn't go negative
                            if (customer.TotalPurchases < 0)
                            {
                                customer.TotalPurchases = 0;
                            }

                            await context.SaveChangesAsync();
                            Debug.WriteLine($"ProdejViewModel: Reverted TotalPurchases for {customer.Email}: -{purchaseAmount:C} = {customer.TotalPurchases:C}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ProdejViewModel: Failed to revert TotalPurchases: {ex.Message}");
                        // Continue with storno even if TotalPurchases update fails
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
                    CashAmount = -originalReceipt.CashAmount,  // NEGATIVE - critical for daily close calculation
                    CardAmount = -originalReceipt.CardAmount,  // NEGATIVE - critical for daily close calculation
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
                    // NOTE: RedeemedGiftCardEan deprecated - now using ReceiptGiftCardRedemptions table
                    // Loyalty fields (for storno, negate amounts)
                    HasLoyaltyDiscount = originalReceipt.HasLoyaltyDiscount,
                    LoyaltyCustomerId = originalReceipt.LoyaltyCustomerId,  // Keep reference for consistency
                    LoyaltyCustomerContact = originalReceipt.LoyaltyCustomerContact ?? string.Empty,
                    LoyaltyDiscountPercent = originalReceipt.LoyaltyDiscountPercent,
                    LoyaltyDiscountAmount = -originalReceipt.LoyaltyDiscountAmount  // NEGATIVE for storno
                };

                // 5. Save storno receipt to DB
                await _dataService.SaveReceiptAsync(stornoReceipt);

                // 6. Remove from cash register (only for cash payments)
                // Check if payment method contains "Hotově" (can be "Hotově" or "Hotově + Dárkový poukaz")
                // Note: AmountToPay already has all discounts subtracted (loyalty + gift card)
                // so we don't need to subtract GiftCardRedemptionAmount again

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
                    // DRY: AmountToPay is the actual cash received (all discounts already subtracted)
                    decimal actualCashReturned = originalReceipt.AmountToPay;

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
