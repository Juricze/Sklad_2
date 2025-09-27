using CommunityToolkit.Mvvm.ComponentModel;
using Sklad_2.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Sklad_2.Services
{
    public partial class ReceiptItem : ObservableObject
    {
        [ObservableProperty]
        private Product product;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPrice))]
        [NotifyPropertyChangedFor(nameof(TotalPriceFormatted))]
        [NotifyPropertyChangedFor(nameof(PriceWithoutVat))]
        [NotifyPropertyChangedFor(nameof(VatAmount))]
        [NotifyPropertyChangedFor(nameof(QuantityFormatted))]
        private int quantity;

        public decimal VatPercentage => Product.VatRate;
        private decimal VatRateAsFraction => VatPercentage / 100m;
        public decimal PriceWithoutVat => TotalPrice / (1 + VatRateAsFraction);
        public decimal VatAmount => TotalPrice - PriceWithoutVat;

        public decimal TotalPrice => Product.SalePrice * Quantity;
        public string TotalPriceFormatted => $"{TotalPrice:C}";
        public string QuantityFormatted => $"{Quantity} ks";
    }

    public partial class ReceiptService : ObservableObject, IReceiptService
    {
        public ObservableCollection<ReceiptItem> Items { get; } = new ObservableCollection<ReceiptItem>();

        public decimal GrandTotal => Items.Sum(i => i.TotalPrice);
        public decimal GrandTotalWithoutVat => Items.Sum(i => i.PriceWithoutVat);
        public decimal GrandTotalVatAmount => Items.Sum(i => i.VatAmount);

        [ObservableProperty]
        private List<ReceiptItem> lastReceiptItems = new List<ReceiptItem>();

        [ObservableProperty]
        private decimal lastReceiptGrandTotal;

        public ReceiptService()
        {
            Items.CollectionChanged += OnItemsChanged;
        }

        private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(GrandTotalWithoutVat));
            OnPropertyChanged(nameof(GrandTotalVatAmount));

            if (e.OldItems != null)
            {
                foreach (ReceiptItem item in e.OldItems)
                {
                    item.PropertyChanged -= OnItemPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (ReceiptItem item in e.NewItems)
                {
                    item.PropertyChanged += OnItemPropertyChanged;
                }
            }
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReceiptItem.Quantity))
            {
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(GrandTotalWithoutVat));
                OnPropertyChanged(nameof(GrandTotalVatAmount));
            }
        }

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