using CommunityToolkit.Mvvm.ComponentModel; 
using Sklad_2.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic; 

namespace Sklad_2.Services
{
    public partial class ReceiptItem : ObservableObject
    {
        [ObservableProperty]
        private Product product;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPrice))]
        [NotifyPropertyChangedFor(nameof(TotalPriceFormatted))]
        [NotifyPropertyChangedFor(nameof(QuantityFormatted))] 
        private int quantity;

        public decimal TotalPrice => Product.SalePrice * Quantity;
        public string TotalPriceFormatted => $"{TotalPrice:C}";
        public string QuantityFormatted => $"{Quantity} ks";
    }

    public partial class ReceiptService : ObservableObject, IReceiptService 
    {
        public ObservableCollection<ReceiptItem> Items { get; } = new ObservableCollection<ReceiptItem>();

        public decimal GrandTotal => Items.Sum(i => i.TotalPrice);

        [ObservableProperty] 
        private List<ReceiptItem> lastReceiptItems = new List<ReceiptItem>();

        [ObservableProperty] 
        private decimal lastReceiptGrandTotal;

        public void AddProduct(Product product)
        {
            if (product == null) return;

            var existingItem = Items.FirstOrDefault(i => i.Product.Ean == product.Ean);
            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                Items.Add(new ReceiptItem { Product = product, Quantity = 1 });
            }
        }

        public void RemoveItem(ReceiptItem item)
        {
            if (item != null)
            {
                Items.Remove(item);
            }
        }

        public void Clear()
        {
            Items.Clear();
        }

        public void FinalizeCurrentReceipt()
        {
            LastReceiptItems = Items.ToList(); 
            LastReceiptGrandTotal = GrandTotal;
        }
    }
}