using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using Sklad_2.Messages;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class VratkyViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;
        private readonly IMessenger _messenger;
        private readonly IAuthService _authService;
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;

        public bool IsVatPayer => _settingsService.CurrentSettings.IsVatPayer;

        [ObservableProperty]
        private string receiptIdToSearch;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsReceiptFound))]
        private Receipt foundReceipt;

        [ObservableProperty]
        private string statusMessage;

        public bool IsReceiptFound => FoundReceipt != null;

        public ObservableCollection<ReturnItemViewModel> ItemsToReturn { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalRefundAmountFormatted))]
        private decimal totalRefundAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalRefundAmountWithoutVatFormatted))]
        private decimal totalRefundAmountWithoutVat;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalRefundVatAmountFormatted))]
        private decimal totalRefundVatAmount;

        public string TotalRefundAmountFormatted => $"{TotalRefundAmount:C}";
        public string TotalRefundAmountWithoutVatFormatted => $"Základ: {TotalRefundAmountWithoutVat:C}";
        public string TotalRefundVatAmountFormatted => $"DPH: {TotalRefundVatAmount:C}";

        [ObservableProperty]
        private Return lastCreatedReturn;

        public VratkyViewModel(IDataService dataService, ISettingsService settingsService, IMessenger messenger, IAuthService authService, IDbContextFactory<DatabaseContext> contextFactory)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _messenger = messenger;
            _authService = authService;
            _contextFactory = contextFactory;

            // Listen for settings changes to update IsVatPayer property
            _messenger.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                OnPropertyChanged(nameof(IsVatPayer));
            });
        }

        [RelayCommand]
        private async Task FindReceiptAsync()
        {
            StatusMessage = string.Empty;
            FoundReceipt = null;
            ItemsToReturn.Clear();

            if (!int.TryParse(ReceiptIdToSearch, out int receiptId))
            {
                StatusMessage = "Zadejte prosím platné číslo účtenky.";
                return;
            }

            var receipt = await _dataService.GetReceiptByIdAsync(receiptId);
            if (receipt == null)
            {
                StatusMessage = $"Účtenka s číslem {receiptId} nebyla nalezena.";
            }
            else
            {
                FoundReceipt = receipt;
                foreach (var item in receipt.Items)
                {
                    var alreadyReturnedQuantity = await _dataService.GetTotalReturnedQuantityForProductOnReceiptAsync(receipt.ReceiptId, item.ProductEan);
                    var returnItemVM = new ReturnItemViewModel(item, alreadyReturnedQuantity);
                    returnItemVM.PropertyChanged += (s, e) => RecalculateTotalRefund();
                    ItemsToReturn.Add(returnItemVM);
                }
                RecalculateTotalRefund();
            }
        }

        private void RecalculateTotalRefund()
        {
            decimal total = 0;
            decimal totalWithoutVat = 0;

            foreach (var item in ItemsToReturn)
            {
                total += item.SubTotal;
                // Recalculate VAT breakdown for the subtotal of items to be returned
                var vatRate = item.OriginalItem.VatRate;
                if (vatRate < 0 || vatRate > 100)
                {
                    // Invalid VAT rate, skip VAT calculation
                    totalWithoutVat += item.SubTotal;
                }
                else
                {
                    var subTotalWithoutVat = item.SubTotal / (1 + (vatRate / 100m));
                    totalWithoutVat += subTotalWithoutVat;
                }
            }

            TotalRefundAmount = total;
            TotalRefundAmountWithoutVat = totalWithoutVat;
            TotalRefundVatAmount = total - totalWithoutVat;
        }

        [RelayCommand]
        private async Task ProcessReturnAsync()
        {
            StatusMessage = string.Empty;
            LastCreatedReturn = null; // Reset
            if (FoundReceipt == null || !ItemsToReturn.Any(i => i.ReturnQuantity > 0))
            {
                StatusMessage = "Není co vracet. Vyhledejte účtenku a zadejte množství k vrácení.";
                return;
            }

            // Validate return quantities
            foreach (var itemVM in ItemsToReturn)
            {
                if (itemVM.ReturnQuantity > itemVM.MaxReturnQuantity)
                {
                    StatusMessage = $"Nelze vrátit více kusů produktu '{itemVM.OriginalItem.ProductName}' (EAN: {itemVM.OriginalItem.ProductEan}) než je povoleno. Maximální počet k vrácení je {itemVM.MaxReturnQuantity}.";
                    return;
                }
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                var returnItems = new List<ReturnItem>();
                decimal totalRefundWithoutVat = 0;
                decimal totalRefundVatAmount = 0;

                foreach (var itemVM in ItemsToReturn.Where(i => i.ReturnQuantity > 0))
                {
                    // Update product stock
                    var product = await _dataService.GetProductAsync(itemVM.OriginalItem.ProductEan);
                    if (product != null)
                    {
                        int stockBefore = product.StockQuantity;
                        product.StockQuantity += itemVM.ReturnQuantity;
                        int stockAfter = product.StockQuantity;
                        await _dataService.UpdateProductAsync(product);

                        // Record stock movement - Return
                        var stockMovement = new StockMovement
                        {
                            ProductEan = product.Ean,
                            ProductName = product.Name,
                            MovementType = StockMovementType.Return,
                            QuantityChange = itemVM.ReturnQuantity,
                            StockBefore = stockBefore,
                            StockAfter = stockAfter,
                            Timestamp = DateTime.Now,
                            UserName = _authService.CurrentUser?.DisplayName ?? "Systém",
                            Notes = $"Vratka k účtence č. {FoundReceipt.ReceiptId}"
                        };
                        await _dataService.AddStockMovementAsync(stockMovement);
                    }

                    // Create return item
                    var totalRefundForItem = itemVM.ReturnQuantity * itemVM.OriginalItem.UnitPrice;
                    var vatRate = itemVM.OriginalItem.VatRate;
                    var priceWithoutVatForItem = (vatRate >= 0 && vatRate <= 100)
                        ? totalRefundForItem / (1 + (vatRate / 100m))
                        : totalRefundForItem; // fallback if VAT rate is invalid
                    var vatAmountForItem = totalRefundForItem - priceWithoutVatForItem;

                    returnItems.Add(new ReturnItem
                    {
                        ProductEan = itemVM.OriginalItem.ProductEan,
                        ProductName = itemVM.OriginalItem.ProductName,
                        ReturnedQuantity = itemVM.ReturnQuantity,
                        UnitPrice = itemVM.OriginalItem.UnitPrice,
                        TotalRefund = totalRefundForItem,
                        VatRate = itemVM.OriginalItem.VatRate,
                        PriceWithoutVat = priceWithoutVatForItem,
                        VatAmount = vatAmountForItem
                    });

                    totalRefundWithoutVat += priceWithoutVatForItem;
                    totalRefundVatAmount += vatAmountForItem;
                }

                // Calculate proportional loyalty discount for returned items
                // If original receipt had 10% discount on 200 Kč total, and we return items worth 100 Kč,
                // the proportional discount is: (100 / 200) * 20 Kč = 10 Kč
                decimal loyaltyDiscountForReturn = 0;
                if (FoundReceipt.HasLoyaltyDiscount && FoundReceipt.TotalAmount > 0 && TotalRefundAmount > 0)
                {
                    decimal proportion = TotalRefundAmount / FoundReceipt.TotalAmount;
                    loyaltyDiscountForReturn = Math.Round(FoundReceipt.LoyaltyDiscountAmount * proportion, 2);
                    Debug.WriteLine($"VratkyViewModel: Proportional loyalty discount: {TotalRefundAmount:C} / {FoundReceipt.TotalAmount:C} * {FoundReceipt.LoyaltyDiscountAmount:C} = {loyaltyDiscountForReturn:C}");
                }

                // Create return document with proper numbering
                int returnYear = DateTime.Now.Year;
                int returnSequence = await _dataService.GetNextReturnSequenceAsync(returnYear);

                var returnDocument = new Return
                {
                    ReturnYear = returnYear,
                    ReturnSequence = returnSequence,
                    ReturnDate = DateTime.Now,
                    OriginalReceiptId = FoundReceipt.ReceiptId,
                    LoyaltyCustomerId = FoundReceipt.LoyaltyCustomerId,  // Copy from original receipt for TotalPurchases tracking
                    LoyaltyDiscountAmount = loyaltyDiscountForReturn,  // Proportional loyalty discount for returned items
                    ShopName = settings.ShopName,
                    ShopAddress = settings.ShopAddress,
                    CompanyId = settings.CompanyId,
                    VatId = settings.VatId ?? string.Empty,  // Empty string for non-VAT payers (NOT NULL constraint)
                    IsVatPayer = settings.IsVatPayer,
                    TotalRefundAmount = TotalRefundAmount,
                    TotalRefundAmountWithoutVat = totalRefundWithoutVat,
                    TotalRefundVatAmount = totalRefundVatAmount,
                    Items = new ObservableCollection<ReturnItem>() // Prázdná kolekce nejprve
                };

                // Přidat items do kolekce po vytvoření entity
                foreach (var item in returnItems)
                {
                    returnDocument.Items.Add(item);
                }

                // Save return document
                await _dataService.SaveReturnAsync(returnDocument);

                // Deduct TotalPurchases from loyalty customer (if original receipt had loyalty customer)
                // DRY: Deduct only what customer actually paid in cash (exclude proportional gift card portion)
                if (FoundReceipt.LoyaltyCustomerId.HasValue && returnDocument.AmountToRefund > 0)
                {
                    try
                    {
                        // Calculate proportional gift card redemption for returned items
                        // If original receipt had 500 Kč gift card on 1000 Kč total, and we return items worth 500 Kč,
                        // the proportional gift card is: (500 / 1000) * 500 Kč = 250 Kč
                        decimal giftCardRedemptionForReturn = 0;
                        if (FoundReceipt.ContainsGiftCardRedemption && FoundReceipt.TotalAmount > 0 && TotalRefundAmount > 0)
                        {
                            decimal proportion = TotalRefundAmount / FoundReceipt.TotalAmount;
                            giftCardRedemptionForReturn = Math.Round(FoundReceipt.GiftCardRedemptionAmount * proportion, 2);
                            Debug.WriteLine($"VratkyViewModel: Proportional gift card redemption: {TotalRefundAmount:C} / {FoundReceipt.TotalAmount:C} * {FoundReceipt.GiftCardRedemptionAmount:C} = {giftCardRedemptionForReturn:C}");
                        }

                        // Deduct only the cash portion (AmountToRefund minus proportional gift card)
                        decimal cashPortionToDeduct = returnDocument.AmountToRefund - giftCardRedemptionForReturn;

                        using var context = await _contextFactory.CreateDbContextAsync();
                        var customer = await context.LoyaltyCustomers.FirstOrDefaultAsync(c => c.Id == FoundReceipt.LoyaltyCustomerId.Value);
                        if (customer != null && cashPortionToDeduct > 0)
                        {
                            customer.TotalPurchases -= cashPortionToDeduct;

                            // Ensure TotalPurchases doesn't go negative
                            if (customer.TotalPurchases < 0)
                            {
                                customer.TotalPurchases = 0;
                            }

                            await context.SaveChangesAsync();
                            Debug.WriteLine($"VratkyViewModel: Deducted TotalPurchases for {customer.Email}: -{cashPortionToDeduct:C} (cash portion) = {customer.TotalPurchases:C}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"VratkyViewModel: Failed to deduct TotalPurchases: {ex.Message}");
                        // Continue - return was already saved successfully
                    }
                }

                StatusMessage = $"Vratka pro účtenku č. {FoundReceipt.ReceiptId} byla úspěšně vytvořena.";
                LastCreatedReturn = returnDocument;
                
                // Clear UI
                FoundReceipt = null;
                ItemsToReturn.Clear();
                ReceiptIdToSearch = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při zpracování vratky: {ex.Message}";
            }
        }
    }

    public partial class ReturnItemViewModel : ObservableObject
    {
        public Models.ReceiptItem OriginalItem { get; }

        [ObservableProperty]
        private int maxReturnQuantity;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubTotal))]
        private int returnQuantity;

        public decimal SubTotal => ReturnQuantity * OriginalItem.UnitPrice;
        public string SubTotalFormatted => $"{SubTotal:C}";

        public ReturnItemViewModel(Models.ReceiptItem originalItem, int alreadyReturnedQuantity)
        {
            OriginalItem = originalItem;
            MaxReturnQuantity = originalItem.Quantity - alreadyReturnedQuantity;
            // Default to returning zero quantity, but not more than MaxReturnQuantity
            ReturnQuantity = 0;
        }
    }
}