using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class ProdejViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
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
        private ReceiptItem selectedReceiptItem;

        [ObservableProperty]
        private string checkoutStatusMessage; 

        public bool IsProductFound => ScannedProduct != null;
        private bool CanManipulateItem => SelectedReceiptItem != null;

        public string ScannedProductPriceFormatted => ScannedProduct != null ? $"{ScannedProduct.SalePrice:C}" : string.Empty;
        public string GrandTotalFormatted => $"Celkem: {Receipt.GrandTotal:C}";

        public ProdejViewModel(IDataService dataService, IReceiptService receiptService, IPrintService printService) 
        {
            _dataService = dataService;
            _printService = printService; 
            Receipt = receiptService;
            Receipt.Items.CollectionChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(GrandTotalFormatted));
                if (e.NewItems != null)
                {
                    foreach (ReceiptItem item in e.NewItems)
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
            CheckoutStatusMessage = string.Empty; 
            try
            {
                foreach (var item in Receipt.Items)
                {
                    var productInDb = await _dataService.GetProductAsync(item.Product.Ean);
                    if (productInDb != null)
                    {
                        if (productInDb.StockQuantity >= item.Quantity)
                        {
                            productInDb.StockQuantity -= item.Quantity;
                            await _dataService.UpdateProductAsync(productInDb);
                        }
                        else
                        {
                            CheckoutStatusMessage = $"Nedostatečné zásoby pro produkt {item.Product.Name} (EAN: {item.Product.Ean}). Požadováno: {item.Quantity}, Skladem: {productInDb.StockQuantity}";
                            Debug.WriteLine(CheckoutStatusMessage);
                            return; 
                        }
                    }
                }
                // Printing
                bool printSuccess = await _printService.PrintReceiptAsync(Receipt);
                if (!printSuccess)
                {
                    CheckoutStatusMessage = "Tisk účtenky selhal.";
                    Debug.WriteLine(CheckoutStatusMessage);
                    return; 
                }

                // Finalize receipt (copy to LastReceiptItems)
                Receipt.FinalizeCurrentReceipt(); 

                // Clear receipt only after successful stock deduction AND printing
                Receipt.Clear();
                ScannedProduct = null;
                CheckoutStatusMessage = "Prodej úspěšně dokončen!"; 
                ReprintLastReceiptCommand.NotifyCanExecuteChanged(); // Notify button state change
            }
            catch (Exception ex)
            {
                CheckoutStatusMessage = $"Chyba při dokončení prodeje: {ex.Message}"; 
                Debug.WriteLine(CheckoutStatusMessage);
            }
        }

        [RelayCommand(CanExecute = nameof(CanReprintLastReceipt))]
        private async Task ReprintLastReceiptAsync()
        {
            if (Receipt.LastReceiptItems == null || Receipt.LastReceiptItems.Count == 0)
            {
                return;
            }

            CheckoutStatusMessage = string.Empty; 
            try
            {
                bool printSuccess = await _printService.PrintReceiptAsync(Receipt);
                if (!printSuccess)
                {
                    CheckoutStatusMessage = "Opakovaný tisk účtenky selhal.";
                    Debug.WriteLine(CheckoutStatusMessage);
                    return;
                }
                CheckoutStatusMessage = "Poslední účtenka úspěšně vytištěna znovu!";
            }
            catch (Exception ex)
            {
                CheckoutStatusMessage = $"Chyba při opakovaném tisku: {ex.Message}"; 
                Debug.WriteLine(CheckoutStatusMessage);
            }
        }

        private bool CanReprintLastReceipt()
        {
            return Receipt.LastReceiptItems != null && Receipt.LastReceiptItems.Count > 0;
        }
    }
}
