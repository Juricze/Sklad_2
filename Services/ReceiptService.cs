using CommunityToolkit.Mvvm.ComponentModel;
using Sklad_2.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Sklad_2.Services
{
    /// <summary>
    /// CartItem represents a product in the shopping cart (UI only).
    /// Different from Models.ReceiptItem which is the database entity.
    /// </summary>
    public partial class CartItem : ObservableObject
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

        // Manual discount support
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UnitPrice))]
        [NotifyPropertyChangedFor(nameof(HasDiscount))]
        [NotifyPropertyChangedFor(nameof(HasManualDiscount))]
        [NotifyPropertyChangedFor(nameof(CombinedDiscountPercent))]
        [NotifyPropertyChangedFor(nameof(DiscountPercent))]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(DiscountReason))]
        [NotifyPropertyChangedFor(nameof(ManualDiscountDisplay))]
        [NotifyPropertyChangedFor(nameof(CombinedDiscountDisplay))]
        [NotifyPropertyChangedFor(nameof(TotalPrice))]
        [NotifyPropertyChangedFor(nameof(TotalPriceFormatted))]
        private decimal manualDiscountPercent = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountReason))]
        [NotifyPropertyChangedFor(nameof(ManualDiscountDisplay))]
        private string manualDiscountReason = string.Empty;

        // Price calculations with discount support
        public decimal UnitPrice 
        {
            get
            {
                // Varianta C: Sčítání procent z původní ceny
                decimal totalDiscountPercent = ProductDiscountPercent + ManualDiscountPercent;
                return OriginalUnitPrice * (1 - totalDiscountPercent / 100);
            }
        }

        public decimal OriginalUnitPrice => Product.SalePrice;
        public bool HasDiscount => Product.IsDiscounted || ManualDiscountPercent > 0;
        
        // Product discount properties
        public bool HasProductDiscount => Product.IsDiscounted;
        public decimal ProductDiscountPercent => Product.DiscountPercent ?? 0;
        public string ProductDiscountReason => Product.DiscountReason ?? string.Empty;
        
        // Manual discount properties
        public bool HasManualDiscount => ManualDiscountPercent > 0;
        
        // Combined discount calculations
        public decimal CombinedDiscountPercent 
        {
            get
            {
                // Varianta C: Jednoduché sčítání procent
                return ProductDiscountPercent + ManualDiscountPercent;
            }
        }
        
        public decimal DiscountPercent => CombinedDiscountPercent;
        public decimal DiscountAmount => OriginalUnitPrice - UnitPrice;
        public string DiscountReason 
        {
            get
            {
                if (HasProductDiscount && HasManualDiscount)
                {
                    return $"{ProductDiscountReason} + {ManualDiscountReason}";
                }
                else if (HasManualDiscount)
                {
                    return ManualDiscountReason;
                }
                else if (HasProductDiscount)
                {
                    return ProductDiscountReason;
                }
                return string.Empty;
            }
        }

        public decimal VatPercentage => Product.VatRate;
        private decimal VatRateAsFraction => VatPercentage / 100m;
        public decimal PriceWithoutVat => TotalPrice / (1 + VatRateAsFraction);
        public decimal VatAmount => TotalPrice - PriceWithoutVat;

        public decimal TotalPrice => UnitPrice * Quantity;
        public decimal TotalOriginalPrice => OriginalUnitPrice * Quantity;
        public decimal TotalDiscountAmount => DiscountAmount * Quantity;
        
        // Formatted strings for UI
        public string ProductDiscountDisplay => HasProductDiscount ? $"{ProductDiscountPercent:0.##}% ({ProductDiscountReason})" : string.Empty;
        public string ManualDiscountDisplay => HasManualDiscount ? $"{ManualDiscountPercent:0.##}% ({ManualDiscountReason})" : string.Empty;
        public string CombinedDiscountDisplay => HasDiscount ? $"{CombinedDiscountPercent:0.##}% celkem" : string.Empty;
        public string TotalPriceFormatted => $"{TotalPrice:C}";
        public string QuantityFormatted => $"{Quantity} ks";
    }

    public partial class ReceiptService : ObservableObject, IReceiptService
    {
        public ObservableCollection<CartItem> Items { get; } = new ObservableCollection<CartItem>();

        public decimal GrandTotal => Items.Sum(i => i.TotalPrice);
        public decimal GrandTotalWithoutVat => Items.Sum(i => i.PriceWithoutVat);
        public decimal GrandTotalVatAmount => Items.Sum(i => i.VatAmount);

        [ObservableProperty]
        private List<CartItem> lastReceiptItems = new List<CartItem>();

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
                foreach (CartItem item in e.OldItems)
                {
                    item.PropertyChanged -= OnItemPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (CartItem item in e.NewItems)
                {
                    item.PropertyChanged += OnItemPropertyChanged;
                }
            }
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CartItem.Quantity) || 
                e.PropertyName == nameof(CartItem.ManualDiscountPercent) ||
                e.PropertyName == nameof(CartItem.UnitPrice))
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
                Items.Add(new CartItem { Product = product, Quantity = 1 });
            }
        }

        public void RemoveItem(CartItem item)
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