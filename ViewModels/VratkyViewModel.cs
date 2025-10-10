using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class VratkyViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;
        private readonly ICashRegisterService _cashRegisterService;

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

        public VratkyViewModel(IDataService dataService, ISettingsService settingsService, ICashRegisterService cashRegisterService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _cashRegisterService = cashRegisterService;
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
                        product.StockQuantity += itemVM.ReturnQuantity;
                        await _dataService.UpdateProductAsync(product);
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

                // Create return document
                var returnDocument = new Return
                {
                    ReturnDate = DateTime.Now,
                    OriginalReceiptId = FoundReceipt.ReceiptId,
                    ShopName = settings.ShopName,
                    ShopAddress = settings.ShopAddress,
                    CompanyId = settings.CompanyId,
                    VatId = settings.VatId,
                    IsVatPayer = settings.IsVatPayer,
                    TotalRefundAmount = TotalRefundAmount,
                    TotalRefundAmountWithoutVat = totalRefundWithoutVat,
                    TotalRefundVatAmount = totalRefundVatAmount,
                    Items = new ObservableCollection<ReturnItem>(returnItems)
                };

                // Save return document
                await _dataService.SaveReturnAsync(returnDocument);

                // Record withdrawal from cash register
                await _cashRegisterService.RecordEntryAsync(EntryType.Return, TotalRefundAmount, $"Vratka k účtence č. {FoundReceipt.ReceiptId}");
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send<Messages.CashRegisterUpdatedMessage, string>(new Messages.CashRegisterUpdatedMessage(), "CashRegisterUpdateToken");

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